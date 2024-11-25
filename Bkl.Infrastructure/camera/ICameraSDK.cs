using System.Threading.Tasks;

namespace Bkl.Infrastructure
{
    public interface ICameraSDK
    {
        public Task<ThermalTemperatureResponse[]> ReadAllTemperature();
    }

}
