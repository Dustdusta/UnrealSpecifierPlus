using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JetBrains.Application;
using JetBrains.Util;
using JetBrains.Util.Logging;
using Markdig;

namespace ReSharperPlugin.UnrealSpecifierPlus;

[ShellComponent]
public class UnrealDocProviderComponent
{
    private static readonly ILogger MyLogger = Logger.GetLogger(typeof(UnrealDocProviderComponent));

    public Dictionary<string, string> UnrealSpecifierMarkdownFiles { get; }

    public MarkdownPipeline Pipeline;

    public UnrealDocProviderComponent()
    {
        UnrealSpecifierMarkdownFiles = new Dictionary<string, string>();
        var pathToDocumentation = GetPathToDocumentationFolder();
        MyLogger.Warn("当前插件工作目录" + pathToDocumentation.FullPath);
        foreach (var enumMarkdown in pathToDocumentation.GetChildFiles(
                         "*.md",
                         PathSearchFlags.RecurseIntoSubdirectories)) {
            if (UnrealSpecifierMarkdownFiles != null) {
                try {
                    UnrealSpecifierMarkdownFiles.Add(enumMarkdown.NameWithoutExtension, enumMarkdown.FullPath);
                }
                catch (Exception e) {
                    MyLogger.LogExceptionSilently(e);
                }
            }
        }

        MyLogger.Warn("Documentation Indexed --- " + UnrealSpecifierMarkdownFiles["BlueprintCallable"]);
        Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    }

    private static FileSystemPath GetPathToDocumentationFolder()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyDir = Path.GetDirectoryName(assembly.Location);
        if (assemblyDir == null)
            return FileSystemPath.Empty;

        var editorPluginPathFile = FileSystemPath.TryParse(assemblyDir).Parent.Combine("documentation").Combine("Specifier");
        return editorPluginPathFile;
    }
}