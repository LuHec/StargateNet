namespace StargateNet
{
    public interface IClientSimulationCallbacks
    {
        public void OnPreRollBack();

        public void OnPostResimulation();
    }
}