using System;
using System.Threading;
using System.Threading.Tasks;
using Companion.Models;
using Renci.SshNet;
using Serilog;

namespace Companion.Services;

public class GlobalSettingsService : IGlobalSettingsService
    {
        private readonly ILogger _logger;
        private readonly ISshClientService _sshClientService;

        public bool IsWfbYamlEnabled { get; private set; } = false;

        public GlobalSettingsService(ILogger logger, ISshClientService sshClientService)
        {
            _logger = logger.ForContext<GlobalSettingsService>();
            _sshClientService = sshClientService;
        }

        public async Task ReadDevice()
        {
            var cts = new CancellationTokenSource(30000); // 30 seconds timeout

            try
            {
                if (DeviceConfig.Instance.DeviceType != DeviceType.None)
                {
                    await CheckWfbYamlSupport(cts.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error reading device configuration");
            }
            finally
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        private async Task CheckWfbYamlSupport(CancellationToken cancellationToken)
        {
            try
            {
                var cmdResult = await GetIsWfbYamlSupported(cancellationToken);
                
                var rawResult = cmdResult?.Result ?? string.Empty;
                IsWfbYamlEnabled = bool.TryParse(Utilities.RemoveLastChar(rawResult), out var result) && result;

                _logger.Debug($"WFB YAML support status: {IsWfbYamlEnabled}");
            }
            catch (Exception ex)
            {
                _logger.Error("Error checking WFB YAML support: " + ex.Message);
                IsWfbYamlEnabled = false;
            }
        }

        private async Task<SshCommand?> GetIsWfbYamlSupported(CancellationToken cancellationToken)
        {
            var command = "test -f /etc/wfb.yaml && echo 'true' || echo 'false'";
            return await _sshClientService.ExecuteCommandWithResponseAsync(
                DeviceConfig.Instance,
                command,
                cancellationToken);
        }
    }
