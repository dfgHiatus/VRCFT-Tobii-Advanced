using System.Runtime.InteropServices;
using Tobii.StreamEngine;
using VRCFaceTracking.Core.Types;

namespace VRCFT_Tobii_Advanced.Tobii;

public class DesktopConsumer : ITobiiDataSource
{
    private readonly nint _device;
    private bool _isSubscribed;
    private EyeData _eyeData;

    public DesktopConsumer(nint device)
    {
        _device = device;
    }

    public void Subscribe()
    {
        var ptr = GCHandle.Alloc(this);

        var res =
            Interop.tobii_gaze_point_subscribe(_device, UpdateData, GCHandle.ToIntPtr(ptr));
        if (res != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            throw new Exception("Could not subscribe to tobii device: " + res);
        }

        _isSubscribed = true;
    }

    public void Unsubscribe()
    {
        _isSubscribed = false;

        var res = Interop.tobii_gaze_point_unsubscribe(_device);
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
            throw new Exception("Wait for callbacks: " + res);
        }

        res = Interop.tobii_device_process_callbacks(_device);
        if (res != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            throw new Exception("Process callbacks: " + res);
        }
    }


    public EyeData GetEyeData()
    {
        return _eyeData;
    }

    private static void UpdateData(ref tobii_gaze_point_t data, nint userData)
    {
        var validity = data.validity == tobii_validity_t.TOBII_VALIDITY_VALID;
        var dir = new Vector2(
                Math.Clamp((2f * data.position.x) -1f, -1f, 1f), // 0..1 to -1..1
                Math.Clamp((-2f * data.position.y) + 1f, -1f, 1f)); // 1..0 to -1..1

        var left = new EyeData.Eye
        {
            GlazeDirectionIsValid = validity,
            GlazeDirection = dir,
        };

        var right = new EyeData.Eye
        {
            GlazeDirectionIsValid = validity,
            GlazeDirection = dir,
        };

        var target = GCHandle.FromIntPtr(userData).Target;
        if (target is DesktopConsumer dev)
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