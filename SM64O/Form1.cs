using WebSocketSharp;
using WebSocketSharp.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SM64O
{
    public partial class Form1 : Form
    {
        public const int MAJOR_VERSION = 0;
        public const int MINOR_VERSION = 4;
        private const int UPDATE_RATE = 24;
        public const int MAX_CHAT_LENGTH = 24;
        public const int HANDSHAKE_LENGTH = MAX_CHAT_LENGTH + 5;

        public static readonly string BASE_DIR = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string PATCHES_DIR = BASE_DIR + "/Patches/";
        public static readonly string RESSOURCES_DIR = BASE_DIR + "/Ressources/";
        //your bannde
        public const string BANDS_FILE = "bans.txt";

        public static WebSocketServer server = null;
        public static ServerConnection connection = null;
        public static Form1 Form;
        private string _bands = "";

        public bool ChatEnabled = true;
        public int MaxPlayers = 24;

        private IEmulatorAccessor _memory;

        private Task _mainTask;
        private UPnPWrapper _upnp;
        private bool _closing;

        public Form1()
        {
            Form = this;

            InitializeComponent();

            _upnp = new UPnPWrapper();
            _upnp.Available += UpnpOnAvailable;
            if (_upnp.UPnPAvailable)
                UpnpOnAvailable(_upnp, EventArgs.Empty);
            _upnp.Initialize();

            //if Patches does not exist, make it!
            Directory.CreateDirectory(PATCHES_DIR);
            string[] fileEntries = Directory.GetFiles(PATCHES_DIR);

            foreach (var file in fileEntries)
            {
                string fileName = Path.GetFileName(file); //file is the full path of the file, fileName is only the file's name (with extension)
                string ressourcesParallel = RESSOURCES_DIR + fileName;
                byte[] buffer = File.ReadAllBytes(file);

                //this version allows overwriting existing files in Ressources. Not uncommenting just in case this method is unfavored.
                //File.Copy(file, ressourcesParallel, true);

                if (File.Exists(ressourcesParallel))
                {
                    File.Delete(ressourcesParallel);
                }

                for (int i = 0; i < buffer.Length; i += 4)
                {
                    byte[] newBuffer = buffer.Skip(i).Take(4).ToArray();
                    newBuffer = newBuffer.Reverse().ToArray();
                    using (var fs = new FileStream(ressourcesParallel, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        fs.Seek(i, SeekOrigin.Current);
                        fs.Write(newBuffer, 0, newBuffer.Length);
                    }
                }
            }

            //moving this outside so we don't read the bans file for every file in the patches directory!
            if (File.Exists(BANDS_FILE))
            {
                foreach (var line in File.ReadAllLines(BANDS_FILE))
                {
                    _bands += line + " ";
                }
            }

            // TODO: Change this according to OS
            _memory = new WindowsEmulatorAccessor();

            this.Text = "Net64 Tool v1.3.1 Hotfix";
        }

        private void UpnpOnAvailable(object o, EventArgs eventArgs)
        {
            if (checkBoxServer.Checked)
            {
                textBoxAddress.Text = _upnp.GetExternalIp();
            }

            toolStripStatusLabel1.Text = "Universal Plug 'n' Play is available!";
        }

        private void die(string msg)
        {
            MessageBox.Show(null, msg, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }

        private string getRomName()
        {
            string romname = null;
            byte[] buffer = new byte[64];

            switch (comboBoxEmulator.Text)
            {
                case "Project64":
                    // Super Mario 64 (u) - Project 64 v2.3.3
                    string windowName = _memory.WindowName;


                    for (int i = windowName.Length - 1; i >= 0; i--)
                    {
                        if (windowName[i] == '-')
                        {
                            romname = windowName.Substring(0, i).Trim();
                            break;
                        }
                    }
                    break;
                case "Mupen64":
                    string wndname = _memory.WindowName;

                    for (int i = wndname.Length - 1; i >= 0; i--)
                    {
                        if (wndname[i] == '-')
                        {
                            romname = wndname.Substring(0, i).Trim();
                            break;
                        }
                    }
                    break;
                case "Nemu64":
                    _memory.ReadMemoryAbs(_memory.MainModuleAddress + 0x3C8A10C, buffer, buffer.Length);

                    romname = Program.GetASCIIString(buffer, 0, Array.IndexOf(buffer, (byte)0));
                    break;
                case "Mupen64+":
                    {
                        _memory.ReadMemoryAbs(_memory.GetModuleBaseAddress("mupen64plus.dll") + 0x1751CA0, buffer, buffer.Length);

                        romname = Program.GetASCIIString(buffer, 0, Array.IndexOf(buffer, (byte)0));
                    }
                    break;
            }

            return romname;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            buttonJoin.Enabled = false;

            backgroundPanel.Enabled = false;

            try
            {

                Task memoryRead = null;
                switch (comboBoxEmulator.Text)
                {
                    case "Project64":
                        memoryRead = Task.Run(() => _memory.Open("Project64"));
                        break;
                    case "Nemu64":
                        memoryRead = Task.Run(() => _memory.Open("Nemu64"));
                        break;
                    case "Mupen64":
                        memoryRead = Task.Run(() => _memory.Open("Mupen64"));
                        break;
                    case "Mupen64+":
                        memoryRead = Task.Run(() => _memory.Open("mupen64plus-ui-console", 32));
                        break;
                    default:
                        die("No emulator was chosen. This should never happen. Yell at Guad if you can see this!");
                        return;
                }

                toolStripStatusLabel1.Text = "Scanning emulator memory...";

                await memoryRead;
            }
            catch (IndexOutOfRangeException)
            {
                die("Your emulator is not running!");
                return;
            }

            try
            {
                if (checkBoxServer.Checked)
                {
                    textBoxAddress.Text = "";
                    int port = (int)numericUpDown2.Value;

                    if (_upnp.UPnPAvailable && !checkBoxLAN.Enabled)
                    {
                        _upnp.AddPortRule(port, false, "Net64");
                        textBoxAddress.Text = _upnp.GetExternalIp();
                    }

                    server = new WebSocketServer(port);
                    server.AddWebSocketService<WebSocketConnection>("/");

                    toolStripStatusLabel1.Text = "Starting server...";

                    server.Start();

                    if (!checkBoxLAN.Checked)
                    {
                        // TODO add port check
                        /*
                        toolStripStatusLabel1.Text = "Querying Net64 port service...";
                        bool success = await NetworkHelper.RequestAssistance(port);

                        if (success)
                        {
                            if (string.IsNullOrEmpty(textBoxAddress.Text) && !string.IsNullOrEmpty(NetworkHelper.ExternalIp))
                                textBoxAddress.Text = NetworkHelper.ExternalIp;
                        }
                        else
                        {
                            var result = MessageBox.Show(this,
                                "Your ports do not seem to be forwarded!\n" +
                                "Nobody outside of your local network will be able to connect. " +
                                "If you would like to play on LAN, tick the checkbox.\n\nWould you like to continue anyways?", "Attention",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                            if (result == DialogResult.No)
                            {
                                Application.Exit();
                                return;
                            }
                        }
                        */
                    }

                    AddChatMessage("[SERVER]", "Started");
                    panel2.Enabled = true;

                    Characters.setMessage("server created", _memory);
                }

                toolStripStatusLabel1.Text = "Connecting to server...";
                string text = checkBoxServer.Checked ? "127.0.0.1" : textBoxAddress.Text.Trim();
                connection = new ServerConnection(text, numericUpDown2.Value.ToString(), _memory, this);

                connection.SetCharacter(comboBoxChar.SelectedIndex);

                labelPlayersOnline.Text = "Chat Log:";

                pingTimer.Start();
            }
            catch (Exception ex)
            {
                // TODO: add logging 
                die("Could not connect/start server:\n" + ex.Message + "\n\nMore info:\n" + ex);
                return;
            }

            timer1_Tick();
            loadPatches();

            checkBoxServer.Enabled = false;
            buttonJoin.Enabled = false;
            chatBox.Enabled = true;
            buttonChat.Enabled = true;
            numericUpDown2.Enabled = false;
            checkBoxLAN.Enabled = false;
            textBoxAddress.ReadOnly = true;
            comboBoxEmulator.Enabled = false;
            checkBoxChat.Enabled = true;
            buttonReset.Enabled = true;

            toolStripStatusLabel1.Text = "Loaded ROM " + getRomName();

            Settings sets = new Settings();

            sets.LastIp = checkBoxServer.Checked ? "" : textBoxAddress.Text;
            sets.LastPort = (int)numericUpDown2.Value;
            sets.Username = usernameBox.Text;

            sets.LastEmulator = comboBoxEmulator.SelectedIndex;
            sets.LastCharacter = comboBoxChar.SelectedIndex;

            Settings.Save(sets, "settings.xml");

            backgroundPanel.Enabled = true;
        }

        private void loadPatches()
        {
            string[] fileEntries = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "/Ressources/");

            foreach (var file in fileEntries)
            {
                string fname = Path.GetFileName(file);
                int offset;

                if (int.TryParse(fname, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset))
                {
                    byte[] buffer = File.ReadAllBytes(file);
                    _memory.WriteMemory(offset, buffer, buffer.Length);
                }
                else if (fname.Contains('.'))
                {
                    // Treat as Regex
                    int separator = fname.LastIndexOf(".", StringComparison.Ordinal);
                    string regexPattern = fname.Substring(0, separator);
                    string address = fname.Substring(separator + 1, fname.Length - separator - 1);
                    string romname = getRomName();

                    regexPattern = regexPattern.Replace("@", "\\");
                    bool invert = false;

                    if (regexPattern[0] == '!')
                    {
                        regexPattern = regexPattern.Substring(1);
                        invert = true;
                    }

                    if (romname == null)
                        continue;

                    bool isMatch = Regex.IsMatch(romname, regexPattern,
                        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

                    if ((isMatch && !invert) || (!isMatch && invert))
                    {
                        offset = int.Parse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                        byte[] buffer = File.ReadAllBytes(file);
                        _memory.WriteMemory(offset, buffer, buffer.Length);
                    }
                }


            }
        }

        private void timer1_Tick()
        {
            _mainTask = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        connection.SendPlayerData();
                        if (checkBoxServer.Checked) WebSocketConnection.SendPlayerData();
                    }
                    catch (Exception e)
                    {
                        Program.LogException(e);
                        Console.WriteLine(e);
                    }
                    await Task.Delay(UPDATE_RATE);
                }
            });

            _mainTask.ContinueWith((t) =>
            {
                if (t.IsFaulted || t.Exception != null)
                {
                    Program.LogException(t.Exception ?? new Exception("Exception in main loop!"));
                }
            });
        }

        public int GetCharacter()
        {
            return comboBoxChar.SelectedIndex;
        }

        public string GetUsername()
        {
            return usernameBox.Text;
        }

        public void SetUsername(string username)
        {
            usernameBox.Text = username;
        }

        public int GetRamLength(int offset)
        {
            for (int i = 0; i < 0x1024; i += 4)
            {
                byte[] buffer = new byte[4];

                _memory.ReadMemory(offset + i, buffer, buffer.Length);
                if ((buffer[0] | buffer[1] | buffer[2] | buffer[3]) == 0)
                    return i;
            }
            return 0;
        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {
            if (textBoxAddress.Text != "")
            {
                buttonJoin.Enabled = true;
            }
            else
            {
                buttonJoin.Enabled = false;
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxServer.Checked)
            {
                textBoxAddress.Text = "";
                buttonJoin.Text = "Create Server!";
                panel2.Enabled = true;
                textBoxAddress.ReadOnly = true;
                buttonJoin.Enabled = true;
                checkBoxLAN.Enabled = true;

                if (_upnp.UPnPAvailable)
                    textBoxAddress.Text = _upnp.GetExternalIp();
                else textBoxAddress.Text = "";

            }
            else
            {

                buttonJoin.Text = "Connect to server!";
                textBoxAddress.ReadOnly = false;
                textBoxAddress.Text = "";
                panel2.Enabled = false;
                buttonJoin.Enabled = false;
                checkBoxLAN.Enabled = false;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBoxEmulator.SelectedIndex = 0;
            comboBoxChar.SelectedIndex = 0;
            gamemodeBox.SelectedIndex = 0;

            Settings sets = Settings.Load("settings.xml");

            if (sets != null)
            {
                textBoxAddress.Text = sets.LastIp;
                numericUpDown2.Value = sets.LastPort;
                usernameBox.Text = sets.Username;

                comboBoxEmulator.SelectedIndex = sets.LastEmulator;
                comboBoxChar.SelectedIndex = sets.LastCharacter;
            }

            // Create the ToolTip and associate with the Form container.
            ToolTip toolTip1 = new ToolTip();

            // Set up the delays for the ToolTip.
            toolTip1.AutoPopDelay = 2500;
            toolTip1.InitialDelay = 500;
            toolTip1.ReshowDelay = 500;
            // Force the ToolTip text to be displayed whether or not the form is active.
            toolTip1.ShowAlways = true;

            // Set up the ToolTip text for the Buttons, Labels, Checkboxes, Lists.
            toolTip1.SetToolTip(this.labelAddress, "Input the IP Address to the host");
            toolTip1.SetToolTip(this.textBoxAddress, "Input the IP Address to the host");

            toolTip1.SetToolTip(this.labelPort, "Input the port to the host");
            toolTip1.SetToolTip(this.numericUpDown2, "Input the port to the host");

            toolTip1.SetToolTip(this.labelUsername, "Input your username");
            toolTip1.SetToolTip(this.usernameBox, "Input your username");

            toolTip1.SetToolTip(this.checkBoxChat, "Check this to disable the chat in your server");
            toolTip1.SetToolTip(this.checkBoxServer, "Check this if you want to make your own server");
            toolTip1.SetToolTip(this.checkBoxLAN, "Check this to disable UPnP and port checking service");

            toolTip1.SetToolTip(this.labelRateUpdate, "The lower the interval, the faster you request updates from other players");

            toolTip1.SetToolTip(this.labelEmulator, "Select your emulator");
            toolTip1.SetToolTip(this.comboBoxEmulator, "Select your emulator");

            toolTip1.SetToolTip(this.labelChar, "Select your playable character");
            toolTip1.SetToolTip(this.comboBoxChar, "Select your playable character");

            toolTip1.SetToolTip(this.chatBox, "Type your chat messages here");
            toolTip1.SetToolTip(this.buttonChat, "Click here to send your message");

            toolTip1.SetToolTip(this.labelMaxClients, "Max number of allowed connections to your server");
            toolTip1.SetToolTip(this.numUpDownClients, "Max number of allowed connections to your server");

            toolTip1.SetToolTip(this.labelGamemode, "Select your gamemode");
            toolTip1.SetToolTip(this.gamemodeBox, "Select your gamemode");

            toolTip1.SetToolTip(this.labelPlayersOnline, "Lists all players who are connected");
            toolTip1.SetToolTip(this.listBoxPlayers, "Lists all players who are connected");

            toolTip1.SetToolTip(this.textBoxChat, "List all chat messages");

            toolTip1.SetToolTip(this.buttonReset, "Click here to reset your game");
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            MaxPlayers = (int)numUpDownClients.Value;
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // TODO ban
            /*int index = listBoxPlayers.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                // Who did we click on
                Client client = (Client)listBoxPlayers.Items[index];
                int indx = Array.IndexOf(playerClient, client);
                Connection conn = client.Connection;

                if (conn == null) return;

                // That player is long gone, how did this happen? I blame hazel
                if (conn.State == Hazel.ConnectionState.Disconnecting ||
                    conn.State == Hazel.ConnectionState.NotConnected)
                {
                    removePlayer(indx);
                    return;
                }

                // really ghetto
                KickBanForm form = new KickBanForm();
                form.username = listBoxPlayers.Items[index].ToString();

                if (form.ShowDialog() == DialogResult.Yes)
                {
                    sendChatTo("kicked", conn);
                    conn.Close();

                    removePlayer(indx);
                }
                else if (form.ShowDialog() == DialogResult.No)
                {
                    sendChatTo("banned", conn);
                    _bands += conn.EndPoint.ToString().Split(':')[0] + Environment.NewLine;
                    File.AppendAllText("bans.txt", conn.EndPoint.ToString().Split(':')[0] + Environment.NewLine);
                    conn.Close();

                    removePlayer(indx);
                }

                labelPlayersOnline.Text = "Players Online: " + playerClient.Count(c => c != null) + "/" +
                                     playerClient.Length;
            }*/
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (ChatEnabled)
            {
                if (string.IsNullOrWhiteSpace(chatBox.Text)) return;
                connection.SendAllChat(usernameBox.Text, chatBox.Text);
                chatBox.Text = "";
            }
        }

        private void chatBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && ChatEnabled)
            {
                if (string.IsNullOrWhiteSpace(chatBox.Text)) return;
                connection.SendAllChat(usernameBox.Text, chatBox.Text);
                chatBox.Text = "";
            }
        }


        private void gamemodeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // TODO game mode needs to be implemented anyway
            //setGamemode();
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            ChatEnabled = !checkBoxChat.Checked;
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (connection == null)
                return; // We are not in a server yet

            connection.SetCharacter(comboBoxChar.SelectedIndex);
            connection.SendPacket(PacketType.CharacterSwitch, new byte[] { (byte)(comboBoxChar.SelectedIndex) });
        }

        private void removeAllPlayers()
        {
            const int maxPlayers = 27;
            for (int i = 0; i < maxPlayers; i++)
            {
                const int playersPositionsStart = 0x36790C;
                const int playerPositionsSize = 0x100;

                // 0xc800
                byte[] buffer = new byte[] { 0x00, 0x00, 0x00, 0xFD };
                _memory.WriteMemory(playersPositionsStart + (playerPositionsSize * i), buffer, buffer.Length);
            }
        }

        private void resetGame()
        {
            if (!_memory.Attached) return;

            byte[] buffer = new byte[4];

            buffer[0] = 0x00;
            buffer[1] = 0x00;
            buffer[2] = 0x04;
            buffer[3] = 0x00;

            _memory.WriteMemory(0x33b238, buffer, buffer.Length);


            buffer[0] = 0x00;
            buffer[1] = 0x00;
            buffer[2] = 0x01;
            buffer[3] = 0x01;

            _memory.WriteMemory(0x33b248, buffer, buffer.Length);

            buffer[0] = 0x00;
            buffer[1] = 0x00;
            buffer[2] = 0x00;
            buffer[3] = 0x00;

            _memory.WriteMemory(0x38eee0, buffer, buffer.Length);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            removeAllPlayers();
            closePort();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            resetGame();
        }

        private void closePort()
        {
            if (_upnp != null)
            {
                _upnp.Available -= UpnpOnAvailable;

                _upnp.RemoveOurRules();
                _upnp.StopDiscovery();
            }
        }

        private void pingTimer_Tick(object sender, EventArgs e)
        {
            if (connection != null)
            {
                byte[] buffer = new byte[4];
                Array.Copy(BitConverter.GetBytes(Environment.TickCount), 0, buffer, 0, 4);
                connection.SendPacket(PacketType.RoundtripPing, buffer);
            }
        }
        
        public void SetPing(int ping)
        {
            pingLabel.Text = string.Format("Ping: {0}ms", ping);
        }

        public void AddChatMessage(string sender, string message)
        {
            string timeStamp = DateTime.Now.ToString("HH:mm:ss");
            textBoxChat.AppendText(string.Format(Environment.NewLine + "[{0}] {1}: {2}", timeStamp, sender, message));
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _closing = true;
        }

        private void forumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://sm64o.com/index.php");
        }

        private void discordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://discordapp.com/invite/k9QMFaB");
        }

        private void creditsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string message = ("Super Mario 64 Online Team"
                + Environment.NewLine
                + "Kaze Emanuar"
                + Environment.NewLine
                + "MelonSpeedruns"
                + Environment.NewLine
                + "Guad"
                + Environment.NewLine
                + "merlish"
                + Environment.NewLine
                + Environment.NewLine
                + "Luigi 3D Model created by: "
                + Environment.NewLine
                + "Cjes"
                + Environment.NewLine
                + "GeoshiTheRed"
                + Environment.NewLine
                + Environment.NewLine
                + "Toad, Rosalina and Peach 3D Models created by: "
                + Environment.NewLine
                + "AnkleD"
                + Environment.NewLine
                + Environment.NewLine
                + "New Character 3D Models created by: "
                + Environment.NewLine
                + "Marshivolt"
                + Environment.NewLine
                + Environment.NewLine
                + "Character Head Icons created by: "
                + Environment.NewLine
                + "Quasmok");

            string caption = "Credits";
            MessageBoxButtons buttons = MessageBoxButtons.OK;
            DialogResult result;

            result = MessageBox.Show(message, caption, buttons);
        }
    }
}
