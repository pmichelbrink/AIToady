using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace AIToady.Infrastructure.Services
{
    public class ConnectionMonitor
    {
        private bool _isConnected = NetworkInterface.GetIsNetworkAvailable();
        
        public event Action ConnectionLost;
        public event Action ConnectionRestored;
        
        public bool IsConnected => _isConnected;
        
        public void Start()
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        }
        
        public void Stop()
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        }
        
        private void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            if (_isConnected && !e.IsAvailable)
            {
                _isConnected = false;
                ConnectionLost?.Invoke();
            }
            else if (!_isConnected && e.IsAvailable)
            {
                _isConnected = true;
                ConnectionRestored?.Invoke();
            }
        }
    }
}