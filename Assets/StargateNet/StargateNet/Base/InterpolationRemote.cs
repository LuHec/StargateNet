namespace StargateNet
{
    /// <summary>
    /// 远端同步的插值，只在客户端存在。
    /// 和LocalInterpolation不同，RemoteInterpolation会随着网络变化而变化。为了抵抗loss、lag，会设置一个缓冲区存放snapshot，而不是立刻应用到来的Snapshot
    /// 否则当loss或lag时等超出了一帧fixedUpdate时间的情况下，就很容易出现卡顿等情况(由于snapshot到来晚了，没有目标可以插值，就只能停在原地。在网络延迟波动的状态下，更容易观察到这点，远端玩家看起来一顿一顿的)。
    /// 用buffer存放snapshot，并设置一个插值延迟，延迟几fixed帧去更新，就能尽可能保证客户端看到的画面没有闪现。
    /// </summary>
    public class InterpolationRemote : Interpolation
    {
        public InterpolationRemote(StargateEngine stargateEngine) : base(stargateEngine)
        {
        }

        internal override Tick FromTick { get; }
        internal override Tick ToTick { get; }
        internal override bool HasSnapshot { get; }
        internal override float Alpha { get; }
        internal override float InterpolationTime { get; }
    }
}