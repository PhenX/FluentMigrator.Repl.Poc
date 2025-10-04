using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FluentMigratorRepl.Services
{
    /// <summary>
    /// Proof-of-concept: Roslyn-based IntelliSense completion service
    /// Provides code completion suggestions at cursor position
    /// </summary>
    public class CompletionService
    {
        private readonly IResourceResolver _resourceResolver;
        private readonly IBlazorHttpClientFactory _httpClientFactory;
        private AdhocWorkspace? _workspace;
        private ProjectId? _projectId;
        private DocumentId? _documentId;
        private bool _isInitialized;

        public CompletionService(IResourceResolver resourceResolver, IBlazorHttpClientFactory httpClientFactory)
        {
            _resourceResolver = resourceResolver;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Initialize the Roslyn workspace with required assemblies
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                // Create workspace
                var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
                _workspace = new AdhocWorkspace(host);

                // Create project
                var projectInfo = ProjectInfo.Create(
                    ProjectId.CreateNewId(),
                    VersionStamp.Create(),
                    "MigrationProject",
                    "MigrationProject",
                    LanguageNames.CSharp,
                    compilationOptions: new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary,
                        allowUnsafe: true
                    ),
                    parseOptions: new CSharpParseOptions(LanguageVersion.Latest)
                );

                var project = _workspace.AddProject(projectInfo);
                _projectId = project.Id;

                // Load assembly references
                var references = await LoadMetadataReferencesAsync();
                foreach (var reference in references)
                {
                    project = project.AddMetadataReference(reference);
                }

                // Create a document
                var documentInfo = DocumentInfo.Create(
                    DocumentId.CreateNewId(_projectId),
                    "Migration.cs",
                    loader: TextLoader.From(TextAndVersion.Create(SourceText.From(""), VersionStamp.Create()))
                );

                var document = project.AddDocument(documentInfo);
                _documentId = document.Id;

                _workspace.TryApplyChanges(document.Project.Solution);
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"POC: Failed to initialize completion service: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get completion suggestions at the specified position
        /// </summary>
        public async Task<CompletionResult> GetCompletionsAsync(string code, int position)
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            try
            {
                // Update document with current code
                var document = _workspace!.CurrentSolution.GetDocument(_documentId!);
                var text = SourceText.From(code);
                document = document.WithText(text);
                _workspace.TryApplyChanges(document.Project.Solution);

                // Get completion service
                var completionService = Microsoft.CodeAnalysis.Completion.CompletionService.GetService(document);
                if (completionService == null)
                {
                    return new CompletionResult { Success = false, Message = "Completion service not available" };
                }

                // Get completions at position
                var completions = await completionService.GetCompletionsAsync(document, position);
                if (completions == null || !completions.ItemsList.Any())
                {
                    return new CompletionResult { Success = true, Items = Array.Empty<CompletionItem>() };
                }

                // Convert to simpler format
                var items = completions.ItemsList.Select(item => new CompletionItem
                {
                    Label = item.DisplayText,
                    Kind = MapCompletionKind(item.Tags),
                    Detail = item.InlineDescription,
                    Documentation = item.Properties.TryGetValue("Description", out var desc) ? desc : "",
                    SortText = item.SortText,
                    FilterText = item.FilterText
                }).Take(50).ToArray(); // Limit to 50 items for POC

                return new CompletionResult
                {
                    Success = true,
                    Items = items,
                    Message = $"Found {items.Length} completions (showing top 50)"
                };
            }
            catch (Exception ex)
            {
                return new CompletionResult
                {
                    Success = false,
                    Message = $"Error getting completions: {ex.Message}"
                };
            }
        }

        private string MapCompletionKind(ImmutableArray<string> tags)
        {
            if (tags.Contains("Class")) return "Class";
            if (tags.Contains("Method")) return "Method";
            if (tags.Contains("Property")) return "Property";
            if (tags.Contains("Field")) return "Field";
            if (tags.Contains("Keyword")) return "Keyword";
            if (tags.Contains("Namespace")) return "Module";
            if (tags.Contains("Interface")) return "Interface";
            if (tags.Contains("Enum")) return "Enum";
            return "Text";
        }

        private async Task<List<MetadataReference>> LoadMetadataReferencesAsync()
        {
            var references = new List<MetadataReference>();

            try
            {
                // List of assemblies needed for FluentMigrator
                var assemblyNames = new[]
                {
                    "System.Runtime",
                    "System.Collections",
                    "System.Linq",
                    "FluentMigrator",
                    "FluentMigrator.Abstractions",
                    "Microsoft.Data.Sqlite"
                };

                var httpClient = _httpClientFactory.CreateClient();

                foreach (var assemblyName in assemblyNames)
                {
                    try
                    {
                        var resourceName = await _resourceResolver.ResolveResourceNameAsync(assemblyName + ".dll");
                        if (resourceName != null)
                        {
                            var wasmBytes = await httpClient.GetByteArrayAsync($"/_framework/{resourceName}");
                            var peBytes = WebcilConverterUtils.ConvertFromWebcil(wasmBytes);
                            references.Add(MetadataReference.CreateFromImage(peBytes));
                        }
                    }
                    catch
                    {
                        // Continue if assembly not found
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"POC: Error loading references: {ex.Message}");
            }

            return references;
        }
    }

    public class CompletionResult
    {
        public bool Success { get; set; }
        public CompletionItem[] Items { get; set; } = Array.Empty<CompletionItem>();
        public string Message { get; set; } = "";
    }

    public class CompletionItem
    {
        public string Label { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Detail { get; set; } = "";
        public string Documentation { get; set; } = "";
        public string SortText { get; set; } = "";
        public string FilterText { get; set; } = "";
    }
}
