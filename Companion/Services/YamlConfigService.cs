using System;
using System.Collections.Generic;
using System.IO;
using Serilog;
using YamlDotNet.RepresentationModel;

namespace Companion.Services;

public class YamlConfigService : IYamlConfigService
{
    private readonly ILogger _logger;

    public YamlConfigService(ILogger logger)
    {
        _logger = logger?.ForContext(GetType()) ?? 
                  throw new ArgumentNullException(nameof(logger));
    }

    public void ParseYaml(string content, Dictionary<string, string> yamlConfig)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.Warning("ParseYaml called with empty or null content.");
            return;
        }

        try
        {
            using var reader = new StringReader(content);
            var yaml = new YamlStream();
            yaml.Load(reader);

            if (yaml.Documents.Count == 0)
            {
                _logger.Warning("No documents found in YAML content.");
                return;
            }

            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            foreach (var entry in root.Children)
            {
                ParseYamlNode(entry.Key.ToString(), entry.Value, yamlConfig);
            }

            _logger.Information("YAML content parsed successfully.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to parse YAML content: {Message}", ex.Message);
        }
    }

    public string UpdateYaml(Dictionary<string, string> yamlConfig)
    {
        try
        {
            var yamlStream = new YamlStream();
            var root = new YamlMappingNode();

            foreach (var kvp in yamlConfig) AddOrUpdateYamlNode(root, kvp.Key, kvp.Value);

            yamlStream.Documents.Add(new YamlDocument(root));

            using var writer = new StringWriter();
            yamlStream.Save(writer, assignAnchors: false);  

            // Get the serialized YAML content as a string
            var yamlContent = writer.ToString();

            // Ensure Unix-style line endings
            yamlContent = yamlContent.Replace("\r\n", "\n");
            yamlContent = yamlContent.TrimEnd(new char[] { '\r', '\n', '.' });
            // Manually replace the "..." marker with newline.
            //yamlContent = yamlContent.Replace("...", "");
            

            _logger.Information("YAML content updated successfully.");
            return yamlContent;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to update YAML content: {Message}", ex.Message);
            return string.Empty;
        }
    }

    private void ParseYamlNode(string parentKey, YamlNode node, Dictionary<string, string> yamlConfig)
    {
        if (node is YamlMappingNode mappingNode)
        {
            foreach (var child in mappingNode.Children)
            {
                var childKey = child.Key.ToString();
                ParseYamlNode($"{parentKey}.{childKey}", child.Value, yamlConfig);
            }
        }
        else if (node is YamlScalarNode scalarNode)
        {
            var value = scalarNode.Value;
            if (!string.IsNullOrEmpty(parentKey) && !string.IsNullOrEmpty(value))
            {
                yamlConfig[parentKey] = value;
                _logger.Debug("Parsed YAML key: {Key}, value: {Value}", parentKey, value);
            }
        }
    }

    private void AddOrUpdateYamlNode(YamlMappingNode root, string keyPath, string newValue)
    {
        var keys = keyPath.Split('.');
        var currentNode = root;

        for (var i = 0; i < keys.Length - 1; i++)
        {
            var key = new YamlScalarNode(keys[i]);

            if (!currentNode.Children.ContainsKey(key)) currentNode.Add(key, new YamlMappingNode());

            currentNode = (YamlMappingNode)currentNode.Children[key];
        }

        var lastKey = new YamlScalarNode(keys[^1]);
        currentNode.Children[lastKey] = new YamlScalarNode(newValue);
    }
}