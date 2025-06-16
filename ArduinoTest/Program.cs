using System.IO.Ports;
using Chetch.Messaging;
using Chetch.Arduino;
using Chetch.Arduino.Devices;
using Chetch.Arduino.Devices.Displays;
using System.Threading.Tasks;
using Chetch.Utilities;
using System.Reflection.Metadata;
using Chetch.Arduino.Devices.Infrared;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json;
using XmppDotNet.Xmpp.Muc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace ArduinoTest;

class Program
{
    const int BAUDRATE = 9600;
    const string PATH2DEVICE = "/dev/tty.usb*";
    //const string PATH2DEVICE = "/dev/cu.usb*";

    const byte OLED_DEVICE_ID = 10;
    const byte RECEIVER_ID = 11;
    const byte LGINSIDE_ID = 12;
    const byte LGOUTSIDE_ID = 13;


    static OLEDTextDisplay oled = new OLEDTextDisplay(OLED_DEVICE_ID, "oled");
    static IRTransmitter lgInside = new IRTransmitter(LGINSIDE_ID, "inside");

    static void printStatus(ArduinoBoard board)
    {
        Console.WriteLine("Board millis: {0}", board.Millis);
        Console.WriteLine("Devices: {0}", board.DeviceCount);
        Console.WriteLine("Free memory: {0}", board.FreeMemory);
    }


    static int n = 0;
    static async Task<int> DoCountAsync(bool thro)
    {
        if(thro)throw new Exception("wow");
        
        Task t = Task.Run(() =>{
            n = 0;
            for (int i = 1; i <= 5; i++)
            {
                n = i;
                //Console.WriteLine("Count: {0}", n);
                Thread.Sleep(500);
            }
            
        });
        
        await t;
        return n;
    }

    
    static async Task<Dictionary<String, IRData>> GetDeviceCommands(String deviceName)
    {
        String filename = "commands-" + deviceName.ToLower().Replace(" ", "-") + ".json";
        String content = String.Empty;
        if (File.Exists(filename))
        {
            content = File.ReadAllText(filename);
        }
        else
        {
            String uri = String.Format("http://localhost:8008/api/device-commands?device={0}", deviceName);
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(uri);
                content = await response.Content.ReadAsStringAsync();
            }
            if (!String.IsNullOrEmpty(content))
            {
                File.WriteAllText(filename, content);
            }
        }
        var result = JsonSerializer.Deserialize<List<IRData>>(content);
        Dictionary<String, IRData> commands = result.ToDictionary(x => x.CommandAlias, x => x);
        return commands;
    }


    static async Task Main(string[] args)
    {

        var lgCommands = await GetDeviceCommands("LG Home Theater");
        IRData auxOpt = lgCommands["AuxOpt"];
        IRData function = lgCommands["Function"];

        var bluetoothSequence = new List<IRData>();
        bluetoothSequence.Add(auxOpt);
        bluetoothSequence.Add(function);
        bluetoothSequence.Add(function);


        ArduinoBoard board = new ArduinoBoard("test");
        board.Connection = new ArduinoSerialConnection(PATH2DEVICE, BAUDRATE);

        board.Ready += (sender, ready) =>
        {
            Console.WriteLine("Board is ready: {0}", ready);
            if (ready) printStatus(board);
        };

        board.ErrorReceived += (sender, eargs) =>
        {
            Console.WriteLine("{0} resulted in an error {1}", sender, eargs.Error);
            if (eargs.Error == ArduinoBoard.ErrorCode.DEVICE_ERROR && eargs.ErrorSource != null)
            {
                Console.WriteLine("Specific error is: {0}", ((ArduinoDevice)eargs.ErrorSource).Error);
            }
        };

        
        board.AddDevice(oled);

        board.AddDevice(lgInside);

        IRTransmitter lgOutside = new IRTransmitter(LGOUTSIDE_ID, "outside");

        try
        {
            board.Begin();
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: {0}", e.Message);
        }

        Console.WriteLine("Enter a command");
        var cmd = Console.ReadLine();
        Console.WriteLine("Command entered: {0}", cmd);

        switch (cmd.ToLower())
        {
            case "d":
                //oled.DiplsayPreset(OLEDTextDisplay.DisplayPreset.HELLO_WORLD, 1000);
                //oled.SetReportInterval(1000);
                lgInside.TransmitAsync(bluetoothSequence, 1000);
                break;
        }

        ConsoleHelper.PK("Press a key to end");
        board.End();

    }
}
