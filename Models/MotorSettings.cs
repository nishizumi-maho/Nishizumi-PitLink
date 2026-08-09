namespace NishizumiPitLink.Models;

/// <summary>
/// Mirrors the MOZA Racing SDK motor parameters (mozaAPI.mozaAPI set*). Every field is nullable:
/// null means "leave this parameter untouched" when a preset is applied, since a .mzpreset file
/// only carries the subset of fields the SDK has a confirmed mapping for (see
/// PitHousePresetImporter). Values always come from a Pit House preset, never from user input in
/// this app.
/// </summary>
public class MotorSettings
{
    public int? RoadSensitivity { get; set; }          // 0-10
    public int? FfbStrength { get; set; }               // 0-100 (game force feedback strength)
    public int? LimitWheelSpeed { get; set; }            // 10-100
    public int? SpringStrength { get; set; }             // 0-100 (mechanical return/spring)
    public int? NaturalDamper { get; set; }              // 0-100 (mechanical damping)
    public int? NaturalFriction { get; set; }            // 0-100 (mechanical friction)
    public int? SpeedDamping { get; set; }                // 0-100
    public int? PeakTorque { get; set; }                  // 50-100 (max output torque limit)
    public int? NaturalInertiaRatio { get; set; }         // 100-4000
    public int? NaturalInertia { get; set; }              // 100-500
    public int? SpeedDampingStartPoint { get; set; }      // 0-400
    public int? HandsOffProtection { get; set; }          // 0-1
    public int? FfbReverse { get; set; }                  // 0-1
    public int? LimitAngle { get; set; }                  // 90-2000 (motor limit angle)
    public int? GameMaximumAngle { get; set; }             // 90-LimitAngle

    public Dictionary<string, int>? EqualizerAmp { get; set; }
}
