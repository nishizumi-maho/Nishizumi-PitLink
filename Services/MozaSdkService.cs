using mozaAPI;
using NishizumiPitLink.Models;

namespace NishizumiPitLink.Services;

public class ApplyOutcome
{
    public bool AnyApplied { get; set; }
    public List<string> Failures { get; } = new();
    public bool Success => Failures.Count == 0;
}

/// <summary>
/// Thin wrapper around the MOZA Racing SDK (mozaAPI.mozaAPI static class). The SDK talks to the
/// wheelbase through MOZA Pit House's own device binding (it returns ERRORCODE.PITHOUSENOTREADY
/// when Pit House isn't ready), so Pit House must stay running - this does not replace it, it
/// drives the same motor parameters live, in addition to whatever preset Pit House last loaded.
/// </summary>
public class MozaSdkService : IDisposable
{
    private bool _installed;

    public bool TryInitialize(out string? error)
    {
        try
        {
            mozaAPI.mozaAPI.installMozaSDK();
            _installed = true;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void Dispose()
    {
        if (_installed)
        {
            try { mozaAPI.mozaAPI.removeMozaSDK(); } catch { /* best effort on shutdown */ }
            _installed = false;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>Quick connectivity probe: NORMAL means a wheelbase is bound and Pit House is ready.</summary>
    public ERRORCODE Probe()
    {
        try
        {
            var err = ERRORCODE.NORMAL;
            mozaAPI.mozaAPI.getMotorPeakTorque(ref err);
            return err;
        }
        catch
        {
            return ERRORCODE.NOINSTALLSDK;
        }
    }

    public ApplyOutcome Apply(MotorSettings s)
    {
        var outcome = new ApplyOutcome();

        void TrySet(string name, Func<ERRORCODE> setter)
        {
            try
            {
                var err = setter();
                outcome.AnyApplied = true;
                if (err != ERRORCODE.NORMAL)
                    outcome.Failures.Add($"{name}: {err}");
            }
            catch (Exception ex)
            {
                outcome.Failures.Add($"{name}: {ex.Message}");
            }
        }

        if (s.RoadSensitivity is { } roadSensitivity)
            TrySet(nameof(s.RoadSensitivity), () => mozaAPI.mozaAPI.setMotorRoadSensitivity(roadSensitivity));
        if (s.FfbStrength is { } ffbStrength)
            TrySet(nameof(s.FfbStrength), () => mozaAPI.mozaAPI.setMotorFfbStrength(ffbStrength));
        if (s.LimitWheelSpeed is { } limitWheelSpeed)
            TrySet(nameof(s.LimitWheelSpeed), () => mozaAPI.mozaAPI.setMotorLimitWheelSpeed(limitWheelSpeed));
        if (s.SpringStrength is { } springStrength)
            TrySet(nameof(s.SpringStrength), () => mozaAPI.mozaAPI.setMotorSpringStrength(springStrength));
        if (s.NaturalDamper is { } naturalDamper)
            TrySet(nameof(s.NaturalDamper), () => mozaAPI.mozaAPI.setMotorNaturalDamper(naturalDamper));
        if (s.NaturalFriction is { } naturalFriction)
            TrySet(nameof(s.NaturalFriction), () => mozaAPI.mozaAPI.setMotorNaturalFriction(naturalFriction));
        if (s.SpeedDamping is { } speedDamping)
            TrySet(nameof(s.SpeedDamping), () => mozaAPI.mozaAPI.setMotorSpeedDamping(speedDamping));
        if (s.PeakTorque is { } peakTorque)
            TrySet(nameof(s.PeakTorque), () => mozaAPI.mozaAPI.setMotorPeakTorque(peakTorque));
        if (s.NaturalInertiaRatio is { } naturalInertiaRatio)
            TrySet(nameof(s.NaturalInertiaRatio), () => mozaAPI.mozaAPI.setMotorNaturalInertiaRatio(naturalInertiaRatio));
        if (s.NaturalInertia is { } naturalInertia)
            TrySet(nameof(s.NaturalInertia), () => mozaAPI.mozaAPI.setMotorNaturalInertia(naturalInertia));
        if (s.SpeedDampingStartPoint is { } speedDampingStartPoint)
            TrySet(nameof(s.SpeedDampingStartPoint), () => mozaAPI.mozaAPI.setMotorSpeedDampingStartPoint(speedDampingStartPoint));
        if (s.HandsOffProtection is { } handsOffProtection)
            TrySet(nameof(s.HandsOffProtection), () => mozaAPI.mozaAPI.setMotorHandsOffProtection(handsOffProtection));
        if (s.FfbReverse is { } ffbReverse)
            TrySet(nameof(s.FfbReverse), () => mozaAPI.mozaAPI.setMotorFfbReverse(ffbReverse));
        if (s.LimitAngle is { } limitAngle)
            TrySet(nameof(s.LimitAngle), () => mozaAPI.mozaAPI.setMotorLimitAngle(limitAngle, s.GameMaximumAngle ?? limitAngle));
        if (s.EqualizerAmp is { Count: > 0 } eq)
            TrySet(nameof(s.EqualizerAmp), () => mozaAPI.mozaAPI.setMotorEqualizerAmp(eq));

        return outcome;
    }
}
