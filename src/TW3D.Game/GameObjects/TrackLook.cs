namespace TW3D.GameObjects
{
    /// <summary>
    /// What the body of a block of the track is drawn as (its <c>Tag</c>): the style of its look, a style or a transition
    /// between two (see Shared/RoadLooks), by which the tyres grip it (Car.Grip).
    /// </summary>
    sealed record TrackLook(string Style);
}
