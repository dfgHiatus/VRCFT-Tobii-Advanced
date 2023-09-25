using System.Runtime.InteropServices;
using Tobii.StreamEngine;
using VRCFaceTracking.Core.Types;

namespace VRCFT_Tobii_Advanced.Tobii;

public class DesktopAdvanced : ITobiiDataSource
{
    private readonly nint _device;
    private bool _isSubscribed;
    private EyeData _eyeData;

    public DesktopAdvanced(nint device)
    {
        _device = device;
    }

    public void Subscribe()
    {
        var ptr = GCHandle.Alloc(this);

        var res =
            Interop.tobii_gaze_data_subscribe(_device, UpdateData, GCHandle.ToIntPtr(ptr));
        if (res != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            throw new Exception("Could not subscribe to tobii device: " + res);
        }

        _isSubscribed = true;
    }

    public void Unsubscribe()
    {
        _isSubscribed = false;

        var res = Interop.tobii_gaze_data_unsubscribe(_device);
        if (res != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            throw new Exception("Could not unsubscribe from tobii device: " + res);
        }
    }

    public void Update()
    {
        var res = Interop.tobii_wait_for_callbacks(new[] { _device });
        if (res == tobii_error_t.TOBII_ERROR_TIMED_OUT)
        {
            return;
        }

        if (res != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            throw new Exception("Could not wait for callbacks: " + res);
        }

        res = Interop.tobii_device_process_callbacks(_device);
        if (res != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            throw new Exception("Could not process callbacks: " + res);
        }
    }

    public EyeData GetEyeData()
    {
        return _eyeData;
    }

    private static void UpdateData(ref tobii_gaze_data_t data, nint userData)
    {
        var dataLeft = data.left;
        var left = new EyeData.Eye
        {
            GlazeDirectionIsValid = dataLeft.gaze_point_validity == tobii_validity_t.TOBII_VALIDITY_VALID,
            GlazeDirection = new Vector2(
                Math.Clamp((2f * dataLeft.gaze_point_on_display_normalized_xy.x) - 1f, -1f, 1f), // 0..1 to  -1..1
                Math.Clamp((-2f * dataLeft.gaze_point_on_display_normalized_xy.y) + 1f, -1f, 1f)), // 1..0 to  -1..1
            PupilDiameterIsValid = dataLeft.pupil_validity == tobii_validity_t.TOBII_VALIDITY_VALID,
            PupilDiameterMm = dataLeft.pupil_diameter_mm
        };

        var dataRight = data.right;
        var right = new EyeData.Eye
        {
            GlazeDirectionIsValid = dataRight.gaze_point_validity == tobii_validity_t.TOBII_VALIDITY_VALID,
            GlazeDirection = new Vector2(
                Math.Clamp((2f * dataLeft.gaze_point_on_display_normalized_xy.x) - 1f, -1f, 1f), // 0..1 to  -1..1
                Math.Clamp((-2f * dataLeft.gaze_point_on_display_normalized_xy.y) + 1f, -1f, 1f)), // 1..0 to  -1..1
            PupilDiameterIsValid = dataRight.pupil_validity == tobii_validity_t.TOBII_VALIDITY_VALID,
            PupilDiameterMm = dataRight.pupil_diameter_mm
        };

        var target = GCHandle.FromIntPtr(userData).Target;
        if (target is DesktopAdvanced dev)
        {
            dev._eyeData = new EyeData
            {
                Left = left,
                Right = right,
            };
        }
    }

    public void Dispose()
    {
        if (_isSubscribed)
        {
            Unsubscribe();
        }
    }
}