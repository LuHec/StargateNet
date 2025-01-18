using StargateNet;

public class TestController : FPSController
{
    [NetworkCallBack(nameof(VerticalSpeed), false)]
    public void OnVerticalSpeedChanged(CallbackData callbackData)
    {
        
    }
}