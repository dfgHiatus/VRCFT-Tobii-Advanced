﻿using Microsoft.Extensions.Logging;
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

            isWearable = DetermineIfWearableOrDesktop(_device);

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

            isWearable = DetermineIfWearableOrDesktop(_device);
        }

        logger.LogInformation("Subscribed to consumer data.");
        logger.LogInformation($"Using {(isWearable ? "wearable" : "desktop") + " mode"}");
        // GetCapabilities(logger, _device);

        _wearable = isWearable ? new WearableConsumer(_device) : new DesktopConsumer(_device);
        _wearable.Subscribe();
    }

    private bool DetermineIfWearableOrDesktop(nint _device)
    {
        var res = Interop.tobii_capability_supported(
            _device,
            tobii_capability_t.TOBII_CAPABILITY_COMPOUND_STREAM_WEARABLE_EYE_OPENNESS,
            out var supportsWearableEyeOpeness);
        if (res != tobii_error_t.TOBII_ERROR_NO_ERROR || _device == nint.Zero)
        {
            throw new Exception("Failed to get device info: " + res);
        }

        res = Interop.tobii_capability_supported(
            _device,
            tobii_capability_t.TOBII_CAPABILITY_COMPOUND_STREAM_WEARABLE_USER_POSITION_GUIDE_XY,
            out var supportsWearableEyePosition);
        if (res != tobii_error_t.TOBII_ERROR_NO_ERROR || _device == nint.Zero)
        {
            throw new Exception("Failed to get device info: " + res);
        }

        return supportsWearableEyeOpeness && supportsWearableEyePosition;
    }

    private void GetCapabilities(ILogger logger, nint _device)
    {
        var capabilities = Enum.GetValues(typeof(tobii_capability_t));

        foreach (tobii_capability_t capabilitity in capabilities)
        {
            var error = Interop.tobii_capability_supported(
                _device,
                capabilitity,
                out var supported);
            if (error != tobii_error_t.TOBII_ERROR_NO_ERROR)
            {
                throw new Exception("Failed to get device info: " + error);
            }

            logger.LogDebug($"Capability {capabilitity} is supported: {supported}");
        }
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
