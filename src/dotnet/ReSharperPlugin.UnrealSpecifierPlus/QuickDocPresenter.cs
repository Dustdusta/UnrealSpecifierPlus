using System;
using System.IO;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Cpp.QuickDoc;
using JetBrains.ReSharper.Feature.Services.QuickDoc;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Cpp.Lang;
using JetBrains.Application.UI.Components.Theming;
using JetBrains.Diagnostics;
using JetBrains.ReSharper.Feature.Services.QuickDoc.Render;
using JetBrains.ReSharper.Psi.Cpp.Presentation;
using JetBrains.UI.RichText;
using JetBrains.Util;
using JetBrains.Util.Logging;
using Markdig;

namespace ReSharperPlugin.UnrealSpecifierPlus;

public class QuickDocPresenter : CppUE4SpecifiersQuickDocPresenter
{
    private readonly ITheming _Theming;

    private static readonly ILogger MyLogger = Logger.GetLogger(typeof(UnrealDocProviderComponent));

    private readonly string _PassedInSpecifierName;

    private readonly string _PassedInDocLink;

    private readonly MarkdownPipeline _Pipeline;

    public QuickDocPresenter(
            CppXmlDocPresenterBase presenter, CppHighlighterColorCache colorCache, ITheming theming,
            string passedInSpecifierName, string passedInDocLink, MarkdownPipeline pipeline) : base(
            presenter,
            colorCache)
    {
        _Theming = theming;
        _PassedInSpecifierName = passedInSpecifierName;
        _PassedInDocLink = passedInDocLink;
        _Pipeline = pipeline;
    }

    public override QuickDocTitleAndText GetHtml(PsiLanguageType presentationLanguage)
    {
        var title = FormatTitle($"`{_PassedInSpecifierName}`");
        var content = FormatContent(_PassedInDocLink);
        var finalText =
                XmlDocHtmlUtil.BuildHtml(
                        (_, output) =>
                                output.Append(title)
                                      .Append("<br>")
                                      .Append(content),
                        XmlDocHtmlUtil.NavigationStyle.None,
                        _Theming);
        return new QuickDocTitleAndText(finalText, title);
    }

    private RichText FormatContent(string docPath)
    {
        try {
            string content = File.ReadAllText(docPath);
            content = ResolveImagePaths(content, docPath);

            // 提取 frontmatter 并移到末尾
            var frontMatterHtml = ExtractFrontMatter(ref content);

            // frontmatter 追加到正文末尾，一起交给 Markdig 渲染
            content += "\n" + frontMatterHtml;
            return Markdown.ToHtml(content, _Pipeline);
        }
        catch (Exception ex) {
            MyLogger.Warn(ex, "无法转换Markdown内容");
            return RichText.Empty;
        }
    }

    private RichText FormatTitle(string name)
    {
        return Select(name, CppHighlightingAttributeIds.CPP_UE4_REFLECTION_SPECIFIER_NAME_ATTRIBUTE)
                .Append(" - Specifier", TextStyle.Default);
    }

    /// <summary>
    /// 从 markdown 中提取 --- 包裹的 YAML frontmatter，转为 HTML 并从原文中移除。
    /// </summary>
    private static string ExtractFrontMatter(ref string markdown)
    {
        var result = new System.Text.StringBuilder();
        markdown = Regex.Replace(markdown, @"^---\s*\n(.*?)\n---\s*\n", match =>
        {
            var yamlBlock = match.Groups[1].Value;
            var lines = yamlBlock.Split('\n');

            result.Append("<b>MD信息:</b><br>");
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var colonIdx = trimmed.IndexOf(':');
                if (colonIdx <= 0) continue;

                var key = trimmed.Substring(0, colonIdx).Trim();
                var value = trimmed.Substring(colonIdx + 1).Trim().Trim('"');
                if (string.IsNullOrEmpty(value)) continue;

                result.Append("<b>");
                result.Append(key);
                result.Append(":</b> ");
                result.Append(value);
                result.Append("<br>");
            }

            return ""; // 从原文中移除
        }, RegexOptions.Singleline);

        return result.ToString();
    }

    /// <summary>
    /// 将 Markdown 中的相对图片路径替换为绝对 file:// 路径。
    /// </summary>
    private static string ResolveImagePaths(string markdown, string docPath)
    {
        var docDir = Path.GetDirectoryName(docPath);
        if (string.IsNullOrEmpty(docDir))
            return markdown;

        return Regex.Replace(markdown, @"!\[([^\]]*)\]\(([^)]+)\)", match =>
        {
            var alt = match.Groups[1].Value;
            var src = match.Groups[2].Value;

            // 跳过绝对 URL
            if (src.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || src.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || src.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)
                || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return match.Value;

            var absolutePath = Path.GetFullPath(Path.Combine(docDir, src));
            if (!File.Exists(absolutePath))
            {
                MyLogger.Warn($"图片文件不存在: {absolutePath}");
                return match.Value;
            }

            var fileUri = new Uri(absolutePath).AbsoluteUri;
            return $"![{alt}]({fileUri})";
        });
    }
}