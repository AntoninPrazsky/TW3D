namespace FarseerPhysics.Graphics.ScreenSystem
{
    public interface IGameScreen
    {
        string GetTitle();
        string GetDetails();
        string LevelName { get; set; }
    }
}