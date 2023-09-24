using Microsoft.Extensions.Logging;
using Tobii.StreamEngine;

namespace VRCFT_Tobii_Advanced.Tobii;

public class Device : IDisposable
{
    private readonly nint _device;
    private readonly ITobiiDataSource? _wearable;

    public Device(ILogger logger, nint api, string deviceUrl, string license = "")
    {
        bool isWearable = true;
        if (!string.IsNullOrEmpty(license))
        {
            logger.LogInformation("Creating device with license.");

            var licenseResults = new List<tobii_license_validation_result_t>();

            var res = Interop.tobii_device_create_ex(api, deviceUrl,
                Interop.tobii_field_of_use_t.TOBII_FIELD_OF_USE_INTERACTIVE,
                new[] { license }, licenseResults,
                out _device);

            if (res != tobii_error_t.TOBII_ERROR_NO_ERROR || _device == nint.Zero)
            {
                throw new Exception("Failed to create tobii device: " + res);
            }

            isWearable = DetermineIfWearable(_device);

            if (licenseResults.Count > 0 &&
                licenseResults[0] == tobii_license_validation_result_t.TOBII_LICENSE_VALIDATION_RESULT_OK)
            {
                logger.LogInformation("Subscribed to advanced data.");
                logger.LogInformation($"Using {(isWearable ? "wearable" : "desktop") + " mode"}");
                _wearable = isWearable ? new WearableAdvanced(_device) : new DesktopAdvanced(_device);
                _wearable.Subscribe();
                return;
            }

            logger.LogWarning("License validation failed: " + licenseResults[0]);
        }
        else
        {
            var res = Interop.tobii_device_create(api, deviceUrl,
                Interop.tobii_field_of_use_t.TOBII_FIELD_OF_USE_INTERACTIVE, out _device);
            if (res != tobii_error_t.TOBII_ERROR_NO_ERROR || _device == nint.Zero)
            {
                throw new Exception("Failed to create tobii device: " + res);
            }

            isWearable = DetermineIfWearable(_device);
        }

        logger.LogInformation("Subscribed to consumer data.");
        logger.LogInformation($"Using {(isWearable ? "wearable" : "desktop") + " mode"}");

        _wearable = isWearable ? new WearableConsumer(_device) : new DesktopConsumer(_device);
        _wearable.Subscribe();
    }

    private bool DetermineIfWearable(nint _device)
    {
        var res = Interop.tobii_get_device_info(_device, out var info);
        if (res != tobii_error_t.TOBII_ERROR_NO_ERROR || _device == nint.Zero)
        {
            throw new Exception("Failed to get device info: " + res);
        }

        // This will either be HMD or Peripheral
        return info.integration_type == "HMD";
    }

    public void Update()
    {
        _wearable?.Update();
    }

    public void Dispose()
    {
        _wearable?.Dispose();

        tobii_error_t res = Interop.tobii_device_destroy(_device);
        if (res != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            throw new Exception("Failed to destroyed tobii device: " + res);
        }
    }

    public EyeData GetEyeData()
    {
        return _wearable?.GetEyeData() ?? default;
    }
}
