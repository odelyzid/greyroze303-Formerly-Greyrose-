using System.Threading.Tasks;

namespace Greyrose.UI
{
    public class ServerController
    {
        Task _ls;
        Task _ps;
        Task _gs;

        public bool IsRunning { get; private set; }

        public void Start()
        {
            if (IsRunning)
                return;
            Server.StartPatchFileServer();
            _ls = Task.Run(Server.LS);
            _ps = Task.Run(Server.PS);
            _gs = Task.Run(Server.GS);
            IsRunning = true;
        }

        public void Stop()
        {
            if (!IsRunning)
                return;
            Server.StopAll();
            IsRunning = false;
        }
    }
}
