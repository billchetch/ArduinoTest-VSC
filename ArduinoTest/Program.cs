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
    const int BAUDRATE = 9600;
    const string MAC_PATH2DEVICE = "/dev/tty.usb*";
    const string LINUX_PATH2DEVICE = "/dev/serial/by-id/usb-1a86*";

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
        CANBusMonitor board = new CANBusMonitor(4);
        board.Connection = new ArduinoSerialConnection(getPath2Device(), BAUDRATE);

        board.Ready += (sender, ready) => {
            Console.WriteLine("Board is ready: {0}", ready);
            if (ready) printStatus(board);
        };

        board.ErrorReceived += (sender, eargs) => {
            Console.WriteLine("{0} resulted in an error {1}", sender, eargs.Error);
        };

        UInt32[] nodeTotals = new UInt32[board.BusSize];
        int maxDiff = 0;
        board.BusMessageReceived += (sender, eargs) =>
        {
            var msg = eargs.Message;

            if (msg.Type == MessageType.DATA)
            {
                var sent = msg.Get<UInt32>(0);
                var recv = msg.Get<UInt32>(1);
                var total = sent + recv;

                var idx = eargs.NodeID - 1;
                nodeTotals[idx] = total;

                StringBuilder sb = new StringBuilder();
                int diff = 0;
                for (int i = 0; i < nodeTotals.Length; i++)
                {
                    sb.Append(nodeTotals[i]);
                    if (i < nodeTotals.Length - 1) sb.Append(",");

                    if (i > 0)
                    {
                        int ct = (int)nodeTotals[i];
                        int pt = (int)nodeTotals[i - 1];
                        diff += Math.Abs(ct - pt);
                    }   
                }
                if (diff > maxDiff) maxDiff = diff;
                Console.WriteLine("Totals: {0} (Diff = {1}, MaxDiff = {2})", sb.ToString(), diff, maxDiff);
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
                Console.WriteLine("FucK: {0}", e.Message);
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
