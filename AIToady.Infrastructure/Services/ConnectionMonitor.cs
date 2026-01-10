using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace AIToady.Infrastructure.Services
{
    public class ConnectionMonitor
    {
        private bool _isConnected = true;
        private System.Timers.Timer _connectionTimer;
        
        public event Action ConnectionLost;
        public event Action ConnectionRestored;
        
        public bool IsConnected => _isConnected;
        
        public void Start()
        {
            _connectionTimer = new System.Timers.Timer(5000);
            _connectionTimer.Elapsed += async (s, e) => await CheckConnection();
            _connectionTimer.Start();
        }
        
        public void Stop()
        {
            _connectionTimer?.Stop();
            _connectionTimer?.Dispose();
        }
        
        private async Task CheckConnection()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync("8.8.8.8", 3000);
                    bool connected = reply.Status == IPStatus.Success;
                    
                    if (_isConnected && !connected)
                    {
                        _isConnected = false;
                        ConnectionLost?.Invoke();
                    }
                    else if (!_isConnected && connected)
                    {
                        _isConnected = true;
                        ConnectionRestored?.Invoke();
                    }
                }
            }
            catch
            {
                if (_isConnected)
                {
                    _isConnected = false;
                    ConnectionLost?.Invoke();
                }
            }
        }
    }
}