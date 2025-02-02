using System.IO.Ports;
using Chetch.Messaging;
using Chetch.Arduino;
using Chetch.Arduino.Devices;
using System.Threading.Tasks;

namespace ArduinoTest;

class Program
{
    static void printStatus(ArduinoBoard board)
    {
        Console.WriteLine("Board millis: {0}", board. Millis);
        Console.WriteLine("Devices: {0}", board. DeviceCount);
        Console.WriteLine("Free memory: {0}", board.FreeMemory);
    }

    static async Task Main(string[] args)
    {
        //ConsoleHelper.PK("Press a key to start");
       
        //foo();

        //ArduinoBoard board = new ArduinoBoard(0x0043, 9600, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        ArduinoBoard board = new ArduinoBoard("first", 0x7523, 9600); //, Frame.FrameSchema.SMALL_NO_CHECKSUM);
        board.Ready += (sender, ready) => {
            Console.WriteLine("Board is ready: {0}", ready);
            if(ready)printStatus(board);
        };

        board.ErrorReceived += (sender, eargs) => {
            Console.WriteLine("{0} resulted in an error {1}", sender, eargs.Error);
            if(eargs.Error == ArduinoBoard.ErrorCode.DEVICE_ERROR && eargs.ErrorSource != null)
            {
                Console.WriteLine("Specific error is: {0}", ((ArduinoDevice)eargs.ErrorSource).Error);
            }
        };

        var ticker = new Ticker(10, "testDevice01");
        ticker.Updated += (sender, props) =>{
            Console.WriteLine("Ticker count is: {0}", ticker.Count);
        };
        //board.AddDevice(ticker);
        
        var switchDevice = new SwitchDevice(11, "glob");
        switchDevice.Switched += (sender, pinState) => {
            Console.WriteLine("{0} has pin state {1}", switchDevice.Name, switchDevice.PinState);
        };
        //board.AddDevice(switchDevice);

        //ConsoleHelper.PK("Press a key to begin");
        
        await Task.Run(()=>{
            try{
                board.Begin();
            } catch (Exception e)
            {
                Console.WriteLine("FucK: {0}", e.Message);
            }

            Thread.Sleep(1000); 
            while(!board.IsReady)
            {
                Console.WriteLine("Waiting for board to become ready...");
                Thread.Sleep(3000);
            }
        });
        
        ConsoleHelper.PK("Press a key to send a test thingy");
        switchDevice.TurnOn();

        ConsoleHelper.PK("Press a key to send a test thingy");
        switchDevice.TurnOff();

        ConsoleHelper.PK("Press a key to disconnect");
        board.End();

        ConsoleHelper.PK("Press a key to end");
        
    }
}
