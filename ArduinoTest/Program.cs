using System.IO.Ports;
using Chetch.Messaging;
using Chetch.Arduino;
using Chetch.Utilities;
using Chetch.Arduino.Devices;
using Chetch.Arduino.Devices.Displays;
using System.Threading.Tasks;
using Chetch.Arduino.Boards;
using Chetch.Arduino.Connections;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Chetch.Arduino.Devices.Comms;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Design;

namespace ArduinoTest;

class Program
{
    const int BAUDRATE = 115200;
    const string MAC_PATH2DEVICE = "/dev/tty.usb*";
    const string LINUX_PATH2DEVICE = "/dev/serial/by-id/usb-1a86*";

    static List<String> errorMessages = new List<String>();

    static String getPath2Device()
    {
        if (OperatingSystem.IsLinux())
        {
            return LINUX_PATH2DEVICE;
        }
        else if (OperatingSystem.IsMacOS())
        {
            return MAC_PATH2DEVICE;
        }
        throw new Exception("Cannot provide path 2 device");

    }
    //const string PATH2DEVICE = "/dev/cu.usb*";

    static void printStatus(ArduinoBoard board)
    {
        Console.WriteLine("Board millis: {0}", board.Millis);
        Console.WriteLine("Devices: {0}", board.DeviceCount);
        Console.WriteLine("Free memory: {0}", board.FreeMemory);
    }

    static async Task Main(string[] args)
    {
        //ConsoleHelper.PK("Press a key to start");

        //ArduinoBoard board = new ArduinoBoard(0x0043, 9600, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        //ArduinoBoard board = new ArduinoBoard("first", 0x7523, 9600); //, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        CANBusMonitor board = new CANBusMonitor(1);
        board.Connection = new ArduinoSerialConnection(getPath2Device(), BAUDRATE);
        

        board.Ready += (sender, ready) => {
            Console.WriteLine("Board is ready: {0}", ready);
            if (ready) printStatus(board);
        };

        board.ErrorReceived += (sender, eargs) => {
            Console.WriteLine("{0} resulted in an error {1}", sender, eargs.Error);
            
        };

        board.NodeReady += (sender, node) =>
        {
            Console.WriteLine("Node {0} is ready", node.NodeID);
        };

        board.NodesReady += (sender, ready)=>
        {
            Console.WriteLine("Nodes Ready {0}", ready);
        };

        board.BusMessageReceived += (sender, eargs) =>
        {
            var msg = eargs.Message;
            //Console.WriteLine("Received bus message {0} bytes {1} from {2} dir {3}", msg.Type, eargs.CanData.Length, eargs.NodeID, eargs.Direction);
            if(msg.Type == MessageType.ERROR)
            {
                var node = board.GetNode(eargs.NodeID);
                String emsg = String.Format("Error {0}: {1}, {2}",
                    node.NodeID,
                    node.MCPNode.LastError,
                    Chetch.Utilities.Convert.ToBitString(node.MCPNode.LastErrorData, "-"));
                Console.WriteLine(emsg);

                errorMessages.Add(emsg);
                
            }
        };

        
        //ConsoleHelper.PK("Press a key to begin");
        ConsoleHelper.CLRLF();

        await Task.Run(() =>
        {
            try
            {
                board.Begin();
            }
            catch (Exception e)
            {
                Console.WriteLine("Oh dear: {0}", e.Message);
            }

            Thread.Sleep(1000);
            while (!board.IsReady)
            {
                Console.WriteLine("Waiting for board to become ready...");
                Thread.Sleep(3000);
            }
        });

        System.Timers.Timer timer = new System.Timers.Timer();
        timer.AutoReset = true;
        timer.Interval = 3000;
        timer.Elapsed += (sender, eargs) =>
        {
            ConsoleHelper.CLR("");
            var allNodes = board.GetAllNodes();
            foreach(var nd in allNodes)
            {
                Console.WriteLine("N{0}: NMs={1}, BMC={2}, ECF={3}, LE={4}, LED={5}",
                    nd.NodeID,
                    nd.MCPNode.NodeMillis,
                    nd.BusMessageCount,
                    Chetch.Utilities.Convert.ToBitString(nd.MCPNode.ErrorCodeFlags, "-"),
                    nd.MCPNode.LastError,
                    Chetch.Utilities.Convert.ToBitString(nd.MCPNode.LastErrorData, "-"));

                foreach (var ec in nd.MCPNode.ErrorCounts)
                {
                    Console.WriteLine("  {0} = {1}", ec.Key, ec.Value);
                }
                Console.WriteLine("-----------------------");
            }
        };
        timer.Start();
        
        Int16 testNumber = 0;
        bool endLoop = false;
        do
        {
            Console.WriteLine("Enter test number (X to end): ");
            ConsoleKeyInfo cki = Console.ReadKey(true);
            testNumber = (byte)(cki.Key - 48);
            if (testNumber < 10)
            {
                Console.WriteLine("Running test: {0}", testNumber);
                board.TestBus((byte)testNumber, (Int16)((testNumber - 1)*10));
            }
            else
            {
                switch (cki.Key)
                {
                    case ConsoleKey.R:
                        board.ResumeBus();
                        break;

                    case ConsoleKey.P:
                        board.PauseBus();
                        break;

                    case ConsoleKey.G:
                        board.PingNode(1);
                        break;

                    case ConsoleKey.I:
                        board.InitialiseNodes();
                        break;

                    default:
                        endLoop = true;
                        break;
                }
            }

            
        } while (!endLoop);
        

        ConsoleHelper.PK("Press a key to disconnect");
        board.End();

        ConsoleHelper.PK("Press a key to end");
    }
}
