using System.Collections.Generic;

namespace Companion.Services;

public interface IYamlConfigService
{
    void ParseYaml(string content, Dictionary<string, string> yamlConfig);
    string UpdateYaml(Dictionary<string, string> yamlConfig);
}