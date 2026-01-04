using System.IO.Ports;
using Chetch.Messaging;
using Chetch.Messaging.Attributes;
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
        //ArduinoBoard board = new ArduinoBoard(0x0043, 9600, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        //ArduinoBoard board = new ArduinoBoard("first", 0x7523, 9600); //, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        //CANBusMonitor board = new CANBusMonitor(3);
        CANBusMonitor board = new CANBusMonitor(1);
        board.Connection = new ArduinoSerialConnection(getPath2Device(), BAUDRATE);
        
        /*
        Message msg = MessageParser.Parse(MessageType.ALERT, board.MasterNode, "LastError,NodeID");

        var s = msg.Serialize();

        var msg2 = Message.Deserialize(s);
        */

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

        board.MessageReceived += (sender, msg) =>
        {
            if(msg.Type != MessageType.INFO && msg.Type != MessageType.DATA){
                Console.WriteLine("Received message {0}", msg.Type);
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
        timer.Interval = 1000;
        timer.Elapsed += (sender, eargs) =>
        {
            ConsoleHelper.CLR("");
            var allNodes = board.GetAllNodes();
            foreach(var nd in allNodes)
            {
                var ba = board.BusActivity[nd.NodeID];
                Console.WriteLine("N{0}: NMs={1}, BMC={2}, MPS={3:F1}, MLC={4}, LTC={5}",
                    nd.NodeID,
                    nd.MCPNode.NodeMillis,
                    ba.MessageCount,
                    ba.MessageRate,
                    ba.MaxLatency,
                    ba.Latency);

                Console.WriteLine("SF={0}, EF={1}, ERX={2}, ETX={3}",
                    Chetch.Utilities.Convert.ToBitString(nd.MCPNode.StatusFlags),
                    Chetch.Utilities.Convert.ToBitString(nd.MCPNode.ErrorFlags),
                    nd.MCPNode.RXErrorCount,
                    nd.MCPNode.TXErrorCount);

                Console.WriteLine("LE={0}, LEO={1}, LED={2}",
                    nd.MCPNode.LastError,
                    nd.MCPNode.LastErrorOn.ToString("s"),
                    Chetch.Utilities.Convert.ToBitString(nd.MCPNode.LastErrorData, "-"));

                Console.WriteLine("ECF={0}, LOG={1}",
                    Chetch.Utilities.Convert.ToBitString(nd.MCPNode.ErrorCodeFlags, "-"),
                    nd.MCPNode.ErrorLog.Count);

                Console.WriteLine("RDY={0}, PRE={1}, STA={2}",
                    nd.MCPNode.LastReadyOn.ToString("s"),
                    nd.MCPNode.LastPresenceOn.ToString("s"),
                    nd.MCPNode.LastStatusResponse.ToString("s"));


                foreach (var ec in nd.MCPNode.ErrorCounts)
                {
                    Console.WriteLine("  {0} = {1}", ec.Key, ec.Value);
                }
                Console.WriteLine("-----------------------");
            }
        };
        //timer.Start();
        
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
                        board.PingNodes();
                        break;

                    case ConsoleKey.I:
                        board.InitialiseNodes();
                        break;

                    case ConsoleKey.S:
                        timer.Start();
                        break;

                    case ConsoleKey.T:
                        board.ResetNodes();
                        break;

                    case ConsoleKey.E:
                        board.RaiseError(2, MCP2515.MCP2515ErrorCode.READ_FAIL, 7);
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
