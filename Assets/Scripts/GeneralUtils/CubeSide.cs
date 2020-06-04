namespace SkywardRay {

public enum CubeSide {

    Left,
    Right,
    Up,
    Down,
    Forward,
    Back

}

public static class CubeSideExtensions {

    public static CubeSide Flip (this CubeSide side) => side switch {
        CubeSide.Left => CubeSide.Right,
        CubeSide.Right => CubeSide.Left,
        CubeSide.Up => CubeSide.Down,
        CubeSide.Down => CubeSide.Up,
        CubeSide.Forward => CubeSide.Back,
        CubeSide.Back => CubeSide.Forward,
        _ => side
    };

}

}