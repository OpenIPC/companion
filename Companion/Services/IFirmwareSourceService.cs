using System.Collections.Generic;
using Companion.Models;

namespace Companion.Services;

public interface IFirmwareSourceService
{
    IReadOnlyList<FirmwareSourceDefinition> GetSources();
}
