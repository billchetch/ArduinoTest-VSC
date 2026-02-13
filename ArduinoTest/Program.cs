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

    static void printNodesStatus(CANBusMonitor board, bool clr = true)
    {
        if(clr)ConsoleHelper.CLR("");
            
        var allNodes = board.GetAllNodes();
        Console.WriteLine("Bus {0}: {1} ({2})", board.SID, board.Activity == null ? "N/A" : board.Activity.ToString(), board.MasterNode.BusMessageCount);

        foreach(var nd in allNodes)
        {
            Console.WriteLine("-----------------------");

            Console.WriteLine("N{0}: State={1} (MCPDevice.Ready={2})",
                nd.NodeID,
                nd.NodeState,
                nd.MCPDevice.IsReady);
            
            Console.WriteLine("NMs={0}, BMC={1}, MPS={2:F1}",
                nd.MCPDevice.NodeMillis,
                nd.MCPDevice.MessageCount,
                nd.MCPDevice.MessageRate);

            Console.WriteLine("SF={0}, EF={1}, ERX={2}, ETX={3}",
                Chetch.Utilities.Convert.ToBitString(nd.MCPDevice.StatusFlags),
                Chetch.Utilities.Convert.ToBitString(nd.MCPDevice.ErrorFlags),
                nd.MCPDevice.RXErrorCount,
                nd.MCPDevice.TXErrorCount);

            Console.WriteLine("LE={0}, LEO={1}, LED={2}",
                nd.MCPDevice.LastError,
                nd.MCPDevice.LastErrorOn.ToString("s"),
                Chetch.Utilities.Convert.ToBitString(nd.MCPDevice.LastErrorData, "-"));

            Console.WriteLine("ECF={0}, LOG={1}",
                Chetch.Utilities.Convert.ToBitString(nd.MCPDevice.ErrorCodeFlags, "-"),
                nd.MCPDevice.ErrorLog.Count);

            Console.WriteLine("RDY={0}, PRE={1}, STA={2}, LMSG={3}",
                nd.MCPDevice.LastReadyOn.ToString("s"),
                nd.MCPDevice.LastPresenceOn.ToString("s"),
                nd.MCPDevice.LastStatusResponse.ToString("s"),
                nd.MCPDevice.LastMessage.Type + " on " + nd.MCPDevice.LastMessageOn.ToString("s"));


            foreach (var ec in nd.MCPDevice.ErrorCounts)
            {
                Console.WriteLine("  {0} = {1}", ec.Key, ec.Value);
            }
        }
    }

    static async Task Main(string[] args)
    {
        //ArduinoBoard board = new ArduinoBoard(0x0043, 9600, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        //ArduinoBoard board = new ArduinoBoard("first", 0x7523, 9600); //, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        CANBusMonitor board = new CANBusMonitor();
        SerialPinMaster spin = new SerialPinMaster("spm");
        spin.Ready += (sender, ready) =>
        {
            Console.WriteLine("Serial Pin {0} with interval {1} ready: {2}", spin.Pin, spin.Interval, ready);
        };

        board.AddDevice(spin);
        //CANBusMonitor board = new CANBusMonitor(6);
        //board.AddRemoteNode(new CANBusNode(4));
        
        
        
        board.Connection = new ArduinoSerialConnection(getPath2Device(), BAUDRATE);
        
        //var allNodes = board.GetAllNodes();
        var remoteNodes = board.GetRemoteNodes();
        SwitchGroup switches = new SwitchGroup("switches");
        /*foreach(var nd in remoteNodes)
        {
            ActiveSwitch sw = new ActiveSwitch("sw" + nd.NodeID);
            sw.Switched += (sender, on) =>
            {
                Console.WriteLine("..............Switch {0} on {1}", sw.SID, on);  
            };
            ((ArduinoBoard)nd).AddDevice(sw);
            switches.Add(sw);
        }*/
        
        /*
        Message msg = MessageParser.Parse(MessageType.ALERT, board.MasterNode, "LastError,NodeID");

        var s = msg.Serialize();

        var msg2 = Message.Deserialize(s);
        */

        board.Ready += (sender, ready) => {
            Console.WriteLine("Board is ready: {0}", ready);
            if (ready) printStatus(board);
        };

        board.ErrorReceived += (sender, eargs) => 
        {
            Console.WriteLine("{0} resulted in an error {1}", sender, eargs.Error);
            if(eargs.Error == ArduinoBoard.ErrorCode.DEVICE_ERROR)
            {
                Console.WriteLine("Message: {0}", eargs.ErrorMessage);
            }
        };

        board.ExceptionThrown += (sender, eargs) =>
        {
            //Console.WriteLine("!!! {0} exception: {1}", board.SID, eargs.GetException().Message);
        };

        board.NodeReady += (sender, ready) =>
        {
            ICANBusNode node = (ICANBusNode)sender;
            Console.WriteLine("Node {0} is ready: {1}", node.NodeID, ready);
        };

        board.NodeError += (sender, errorCode) =>
        {
            ICANBusNode node = (ICANBusNode)sender;
            Console.WriteLine("Node {0} errored: {1} {2}", node.NodeID, errorCode, Chetch.Utilities.Convert.ToBitString(node.MCPDevice.LastErrorData, "-"));
        };

        board.NodeStateChanged += (sender, eargs) =>
        {
            Console.WriteLine("@@@@@ Node State Change!: N{0} went from {1} to {2}", eargs.NodeID, eargs.OldState, eargs.NewState);
        };

        board.BusMessageReceived += (sender, eargs) =>
        {
            var msg = eargs.Message;
            if(msg.Type == MessageType.ERROR)
            {
                Console.WriteLine("<<<<<< Received bus message {0} bytes {1} from Node {2} dir {3} and target/sender {4}/{5}", msg.Type, eargs.CanData.Length, eargs.NodeID, eargs.Direction, msg.Target, msg.Sender);
            }
            
        };

        board.MessageReceived += (sender, msg) =>
        {
            if(msg.Type != MessageType.INFO && msg.Type != MessageType.DATA){
                //Console.WriteLine("<----- Received message {0} from Sender {1} with target {2}", msg.Type, msg.Sender, msg.Target);
                /*switch (msg.Type)
                {
                    case MessageType.COMMAND_RESPONSE:
                        Console.WriteLine("Original command: {0}", msg.Get<ArduinoDevice.DeviceCommand>(0));
                        break;
                }*/
            }
        };

        board.MessageSent += (sender, msg) =>
        {
            switch (msg.Type)
            {
                case MessageType.STATUS_REQUEST:
                    Console.WriteLine("-----> Sent message {0} from Sender {1} with target {2}", msg.Type, sender.GetType().ToString(), msg.Target);
                    break;
            
                case MessageType.COMMAND:
                    //Console.WriteLine("Command: {0}", msg.Get<ArduinoDevice.DeviceCommand>(0));
                    break;

                case MessageType.RESET:
                    Console.WriteLine("---------> Sent reset!");
                    break;

                case MessageType.ERROR_TEST:
                    Console.WriteLine("---------> Sent error test!");
                    break;
            }
        };


        
        //ConsoleHelper.PK("Press a key to begin");
        ConsoleHelper.CLRLF();
        await Task.Run(() =>
        {
            try
            {
                Console.WriteLine("------- Beginning {0}....", board.SID);
                board.Begin();
            }
            catch (Exception e)
            {
                Console.WriteLine("Oh dear: {0}", e.Message);
            }
            Console.WriteLine("------- Waiting on {0}....", board.SID);
            
            Thread.Sleep(1000);
            while (!board.IsReady)
            {
                Console.WriteLine("Waiting for board to become ready...");
                Thread.Sleep(3000);
            }
        });
        
        board.MasterNode.BusActivityUpdated += (sender, eargs) =>
        {
            //printNodesStatus(board, true);
        };
        //timer.Start();
        
        Int16 testNumber = 0;
        bool endLoop = false;
        byte data2send = 0;
        do
        {
            Console.WriteLine("Enter test number (X to end): ");
            ConsoleKeyInfo cki = Console.ReadKey(true);
            testNumber = (byte)(cki.Key - 48);
            if (testNumber < 10)
            {
                Console.WriteLine("Running test: {0}", testNumber);
                //board.TestBus((byte)testNumber, (Int16)((testNumber - 1)*10));
            }
            else
            {
                switch (cki.Key)
                {
                    case ConsoleKey.R:
                        board.ResetNode(1);
                        break;

                    case ConsoleKey.P:
                        printNodesStatus(board, true);
                        break;

                    case ConsoleKey.E:
                        board.RaiseNodeError(1, MCP2515.MCP2515ErrorCode.ALL_TX_BUSY, 3);
                        //board.RaiseNodeError(1, 154, 3);
                        break;

                    case ConsoleKey.G:
                        board.PingNode(6);
                        break;

                    
                    case ConsoleKey.S:
                        spin.Send(data2send++);
                        //timer.Start();
                        break;

                    case ConsoleKey.T:
                        switches.TurnOn();
                        break;

                    case ConsoleKey.U:
                        switches.TurnOff();
                        break;

                    case ConsoleKey.V:
                        break;

                    case ConsoleKey.I:
                        board.InitialiseNode(1);
                        break;

                    case ConsoleKey.F:
                        board.FinaliseNode(1);
                        break;

                    default:
                        endLoop = true;
                        break;
                }
            }

            
        } while (!endLoop);
        

        ConsoleHelper.PK("Press a key to disconnect");
        await board.End();

        ConsoleHelper.PK("Press a key to end");
    }
}
