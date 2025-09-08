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

namespace ArduinoTest;

class Program
{
    const int BAUDRATE = 115200;
    const string MAC_PATH2DEVICE = "/dev/tty.usb*";
    const string LINUX_PATH2DEVICE = "/dev/serial/by-id/usb-1a86*";

    public class ReportData
    {
        public byte NodeID = 0;

        byte MaxDiffNode = 0;
        byte MaxDiff = 0;
        byte MaxIdleNode = 0;
        UInt32 MaxIdle = 0;

        byte DiffErrorNode = 0;
        byte DiffError = 0;
        byte LoopTime = 0;
        UInt32 MaxLoopTime = 0;

        UInt32 SentMessages = 0;
        UInt32 ReceivedMessages = 0;

        int tagCount = 0;

        public DateTime CompletedOn;

        public bool Complete => tagCount == 3;

        public ReportData(byte nodeID)
        {
            NodeID = nodeID;
        }

        public bool Read(ArduinoMessage msg)
        {
            if (Complete) return false;

            if (msg.Tag == 1)
            {
                MaxDiffNode = msg.Get<byte>(0);
                MaxDiff = msg.Get<byte>(1);
                MaxIdleNode = msg.Get<byte>(2);
                MaxIdle = msg.Get<UInt32>(3);
                tagCount++;
            }
            else if (msg.Tag == 2)
            {
                DiffErrorNode = msg.Get<byte>(0);
                DiffError = msg.Get<byte>(1);
                LoopTime = msg.Get<byte>(2);
                MaxLoopTime = msg.Get<UInt32>(3);

                tagCount++;
            }
            else if (msg.Tag == 3)
            {
                SentMessages = msg.Get<UInt32>(0);
                ReceivedMessages = msg.Get<UInt32>(1);
            }

            if (Complete)
            {
                CompletedOn = DateTime.Now;
            }
            return true;
        }

        public void Clear()
        {
            tagCount = 0;
            MaxDiffNode = 0;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Report for Node {0} @ {1}", NodeID, CompletedOn.ToString("s"));
            sb.AppendLine();
            sb.AppendFormat(" - MaxDiff: Node {0} -> {1}", MaxDiffNode, MaxDiff);
            sb.AppendLine();
            sb.AppendFormat(" - MaxIdle: Node {0} -> {1}", MaxIdleNode, MaxIdle);
            sb.AppendLine();
            sb.AppendFormat(" - DiffError: Node {0} -> {1}", DiffErrorNode, DiffError);
            sb.AppendLine();
            sb.AppendFormat(" - Loop: Last={0} Max={1}", LoopTime, MaxLoopTime);
            sb.AppendLine();
            sb.AppendFormat(" - Sent/Received: {0} {1}", SentMessages, ReceivedMessages);
            sb.AppendLine();

            return sb.ToString();
        }
    }
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

        //foo();

        //ArduinoBoard board = new ArduinoBoard(0x0043, 9600, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        //ArduinoBoard board = new ArduinoBoard("first", 0x7523, 9600); //, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        CANBusMonitor board = new CANBusMonitor(3);
        board.Connection = new ArduinoSerialConnection(getPath2Device(), BAUDRATE);
        /*board.AddRemoteNode(new CANBusNode(2));
        board.AddRemoteNode(new CANBusNode(3));
        board.AddRemoteNode(new CANBusNode(4));*/
        

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

        RingBuffer<ReportData> log = new RingBuffer<ReportData>(100);

        Dictionary<byte, ReportData> reportData = new Dictionary<byte, ReportData>();
        board.BusMessageReceived += (sender, eargs) =>
        {
            var msg = eargs.Message;

            //Console.WriteLine("Bus message node {0} type {1} tag {2}", eargs.NodeID, eargs.Message.Type, eargs.Message.Tag);
            if (msg.Type == MessageType.INFO)
            {
                try
                {
                    if (!reportData.ContainsKey(eargs.NodeID))
                    {
                        reportData[eargs.NodeID] = new ReportData(eargs.NodeID);
                    }
                    var rd = reportData[eargs.NodeID];
                    rd.Read(msg);
                    if (rd.Complete)
                    {
                        Console.WriteLine(rd.ToString());
                        reportData.Remove(rd.NodeID);
                        log.Add(rd);
                        
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
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
        
        ConsoleHelper.PK("Press a key to disconnect");
        board.End();

        ConsoleHelper.PK("Press a key to end");
        board.End();

    }
}
