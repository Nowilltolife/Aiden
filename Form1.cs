﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Speech;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Speech.AudioFormat;
using System.Diagnostics;
using System.Threading;

namespace Aiden
{
    public partial class Assistant : Form
    {

        private async void FadeIn(Form o, int interval = 80)
        {
            //Object is not fully invisible. Fade it in
            while (o.Opacity < 1.0)
            {
                await Task.Delay(interval);
                o.Opacity += 0.05;
            }
            o.Opacity = 1; //make fully visible       
        }

        private async void FadeOut(Form o, int interval = 80)
        {
            //Object is fully visible. Fade it out
            while (o.Opacity > 0.0)
            {
                await Task.Delay(interval);
                o.Opacity -= 0.05;
            }
            o.Opacity = 0; //make fully invisible       
        }

        SpeechRecognitionEngine _engine = new SpeechRecognitionEngine();
        SpeechSynthesizer aiden = new SpeechSynthesizer();
        SpeechRecognitionEngine start = new SpeechRecognitionEngine();
        Random rnd = new Random();
        List<Protocol> protocols = new List<Protocol>();
        int fadeTime = 10;

        public Assistant()
        {
            InitializeComponent();
        }

        private delegate void SafeCallDelegate();

        private void DisableGIF()
        {
            FadeOut(this, fadeTime);
        }

        private Dictionary<string, string> pathCache = new Dictionary<string, string>();

        public void ExecuteCommandAsync(object command)
        {
            new Thread(() =>
            {
                ExecuteCommandSync(command);
            }).Start();
        }
        public string ExecuteCommandSync(object command)
        {
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                System.Diagnostics.ProcessStartInfo procStartInfo =
                    new System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);

                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;
                // Now we create a process, assign its ProcessStartInfo and start it
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                // Get the output into a string
                string result = proc.StandardOutput.ReadToEnd();
                // Display the command output.
                return result;
            }
            catch (Exception objException)
            {
                // Log the exception
            }
            return "";
        }

        private string findAppPath(string basepath, string appname)
        {

            string res = ExecuteCommandSync("where /R " + basepath + " " + appname);

            return res.Split('\n')[0];

        }

        private void FindPathAndExecute(string basepath, string appname)
        {
            string path = "";

            if (!pathCache.ContainsKey(appname))
            {
                path = findAppPath(@"%HOMEPATH%\AppData\Local", "discord");
                pathCache.Add(appname, path);
            }
            else
                pathCache.TryGetValue(appname, out path);
            if (path != "INFO: Could not find files for the given pattern(s).")
            {

                ExecuteCommandAsync(path);
            }else
            {
                aiden.SpeakAsync("Cant find " + appname);
            }
        }

        private void FindPathAndCache(string basepath, string appname)
        {
            new Thread(() =>
            {
                string path = "";
                 if (!pathCache.ContainsKey(appname))
                {
                    path = findAppPath(@"%HOMEPATH%\AppData\Local", "discord");
                    pathCache.Add(appname, path);
                }
                Console.WriteLine("Finished Cachching path of " + appname);
            }).Start();
        }

        public void CacheAppPaths()
        {
            FindPathAndCache("%HOMEPATH%/AppData/Local", "discord");
        }

        string[] appNames = { "discord", "firefox", "chrome", "settings" };


        private void Form1_Load(object sender, EventArgs e)
        {
            this.TopMost = true;
            this.Opacity = 0;
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            this.Left = workingArea.Left + workingArea.Width - this.Size.Width;
            this.Top = workingArea.Top + workingArea.Height - this.Size.Height;
            protocols.Add(new Proto1());
            protocols.Add(new Proto2());
            protocols.Add(new Proto56());
            protocols.Add(new Proto15());
            protocols.Add(new ProtoChill());
            protocols.Add(new ProtoMarvin());

            CacheAppPaths();

            // Initialize a new instance of the SpeechSynthesizer.  
            aiden.SelectVoiceByHints(VoiceGender.Male);

                Grammar g;
            Choices commandtype = new Choices();
            commandtype.Add(Properties.FileRef.commands.Split(','));
            foreach(Protocol proto in protocols)
            {
                commandtype.Add("execute protocol " + proto.ident);
            }
            foreach(string app in appNames)
            {
                commandtype.Add("open " + app);
            }

            SemanticResultKey srkComtype = new SemanticResultKey("comtype", commandtype.ToGrammarBuilder());


            GrammarBuilder builder = new GrammarBuilder();
            builder.Culture = System.Globalization.CultureInfo.CreateSpecificCulture("en-GB");
            builder.Append(srkComtype);
            builder.AppendDictation();


            g = new Grammar(builder);

            _engine.SetInputToDefaultAudioDevice();
            _engine.LoadGrammarAsync(g);
            _engine.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(onSpeechRec);
            _engine.SpeechDetected += new EventHandler<SpeechDetectedEventArgs>(onSpeechDetect);

            start.SetInputToDefaultAudioDevice();
            start.LoadGrammarAsync(new Grammar(new GrammarBuilder(new Choices("hey aidan", "assistant", "hey assistant"))));
            start.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(onStartSpeechRec);
            start.RecognizeAsync(RecognizeMode.Multiple);


        }

        string ordinal_suffix_of(int i)
        {
            var j = i % 10;
            var k = i % 100;
            if (j == 1 && k != 11)
            {
                return i + "st";
            }
            if (j == 2 && k != 12)
            {
                return i + "nd";
            }
            if (j == 3 && k != 13)
            {
                return i + "rd";
            }
            return i + "th";
        }

        private void onSpeechRec(object sender, SpeechRecognizedEventArgs e)
        {
            string speech = e.Result.Text;

            Console.WriteLine(speech);


            if (speech == "status")
            {
                getStatus();
            }

            bool already = false;

            string[] split = speech.Split(' ');
            switch(split[0])
            {
                case "execute":
                    {
                        switch(split[1])
                        {
                            case "protocol":
                                {

                                    string name = split[2];

                                    foreach(Protocol proto in protocols)
                                    {
                                        if(proto.ident == name)
                                        {
                                            new Thread(() =>
                                            {
                                                proto.execute(aiden, split.Skip(3).ToArray());
                                                this.Invoke(new SafeCallDelegate(DisableGIF), new object[] { });
                                                already = true;
                                            }).Start();
                                            break;
                                        }
                                    }
                                    break;
                                }
                        }
                        break;
                    }
                case "search":
                    {
                        Process.Start("firefox.exe", "duckduckgo.com/?q=" + speech.Substring("search ".Length).Replace(" ", "+"));
                        break;
                    }
                case "fuck":
                case "shut":
                case "stop":
                    {
                        break;
                    }

                case "open":
                    {
                        switch(split[1])
                        {

                            case "discord":
                                {

                                    FindPathAndExecute(@"%HOMEPATH%\AppData\Local", "discord");
                                    break;
                                }

                        }
                        break;
                    }
                case "time":
                    {
                        aiden.SpeakAsync(DateTime.Now.ToString("h m tt"));
                        break;
                    }
                case "date":
                    {
                        aiden.SpeakAsync(DateTime.Now.ToString("dddd") + " the " + ordinal_suffix_of(DateTime.Now.Day) + " of " + DateTime.Now.ToString("MMMM"));
                        break;
                    }

            }

            if (split[0] == "what" && split[1] == "is" && split[2] == "adam")
            {
                aiden.SpeakAsync("a retard");
            }

            if (!already)
            FadeOut(this, fadeTime);
            _engine.RecognizeAsyncCancel();
            start.RecognizeAsync(RecognizeMode.Multiple);
        }

        private void getStatus()
        {

            aiden.SpeakAsync("Status: Normal");

        }

        private void onSpeechDetect(object sender, SpeechDetectedEventArgs e)
        {
        }

        private void onStartSpeechRec(object sender, SpeechRecognizedEventArgs e)
        {

            Console.WriteLine(e.Result.Text);

            if(e.Result.Text == "hey aidan" || e.Result.Text == "assistant" || e.Result.Text == "hey assistant")
            {

                start.RecognizeAsyncCancel();
                //search foraiden.SpeakAsync("I am here");
                _engine.RecognizeAsync(RecognizeMode.Multiple);
                FadeIn(this, fadeTime);

            }

        }

        
    }
}
