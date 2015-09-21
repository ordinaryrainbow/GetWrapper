using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Security.Permissions;

namespace Utilities.CommandLine
{
    [SecurityPermissionAttribute(SecurityAction.LinkDemand, Unrestricted=true)]
    public class CommandLineHelper
    {
        private delegate string StringDelegate();

        public string Run(string fileName, string arguments, 
            out string errorMessage,DataReceivedEventHandler logHandler,DataReceivedEventHandler errorHandler)
        {
            errorMessage = "";
            Process cmdLineProcess = new Process();
            using (cmdLineProcess)
            {
                cmdLineProcess.StartInfo.FileName = fileName;
                cmdLineProcess.StartInfo.Arguments = arguments;
                cmdLineProcess.StartInfo.UseShellExecute = false;
                cmdLineProcess.StartInfo.CreateNoWindow = true;
                cmdLineProcess.StartInfo.WorkingDirectory = @"C:\Program Files (x86)\get_iplayer";
                cmdLineProcess.StartInfo.RedirectStandardOutput = true;
                cmdLineProcess.StartInfo.RedirectStandardError = true;

                if (cmdLineProcess.Start())
                {

                    //cmdLineProcess.OutputDataReceived += new DataReceivedEventHandler(CmdOutputDataReceived);
                    cmdLineProcess.OutputDataReceived += logHandler;
                    cmdLineProcess.ErrorDataReceived += errorHandler;
                    cmdLineProcess.BeginOutputReadLine();

                    //return ReadProcessOutput(cmdLineProcess, ref errorMessage, 
                    //    fileName);

                    return "";



                }
                else
                {
                    throw new CommandLineException(String.Format(
                        "Could not start command line process: {0}", 
                        fileName));
                    /* Note: arguments aren't also shown in the 
                     * exception as they might contain privileged 
                     * information (such as passwords).
                     */
                }
            }
        }

        public static int Run(string fileName, string arguments, Action<string> output /*, TextReader input */)
        {

            if (output == null)
                throw new ArgumentNullException("output");

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.UseShellExecute = false;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardInput = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.CreateNoWindow = true;
            psi.ErrorDialog = false;
            psi.WorkingDirectory = @"c:\program files\get_iplayer";
            psi.FileName = fileName;
            psi.Arguments = arguments;

            using (Process process = Process.Start(psi))
            using (ManualResetEvent mreOut = new ManualResetEvent(false),
            mreErr = new ManualResetEvent(false))
            {
                process.OutputDataReceived += (o, e) => { if (e.Data == null) mreOut.Set(); else output(e.Data); };
                process.BeginOutputReadLine();
                process.ErrorDataReceived += (o, e) => { if (e.Data == null) mreErr.Set(); else output(e.Data); };
                process.BeginErrorReadLine();

                //string line;
                //while (input != null && null != (line = input.ReadLine()))
                //    process.StandardInput.WriteLine(line);

                //process.StandardInput.Close();
                process.WaitForExit();

                mreOut.WaitOne();
                mreErr.WaitOne();
                return process.ExitCode;
            }
        }


        /// <summary>
        /// Standard Output from process
        /// </summary>
        private System.Text.StringBuilder output = new System.Text.StringBuilder();

        /// <summary>
        /// Handles the OutputDataReceived event of the prsProjectTypes control.
        /// </summary>
        /// <param name="sender">The source of the event.
        /// <param name="e">The <see cref="System.Diagnostics.DataReceivedEventArgs"> instance containing the event data.
        private void CmdOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            output.AppendLine(e.Data);
        }

        private static string ReadProcessOutput(Process cmdLineProcess,
                  ref string errorMessage, string fileName)
        {
            StringDelegate outputStreamAsyncReader
               = new StringDelegate(cmdLineProcess.StandardOutput.ReadToEnd);
            StringDelegate errorStreamAsyncReader
               = new StringDelegate(cmdLineProcess.StandardError.ReadToEnd);

            IAsyncResult outAR
                = outputStreamAsyncReader.BeginInvoke(null, null);
            IAsyncResult errAR = errorStreamAsyncReader.BeginInvoke(null, null);

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                /* WaitHandle.WaitAll fails on single-threaded 
                 * apartments. Poll for completion instead:
                 */
                while (!(outAR.IsCompleted && errAR.IsCompleted))
                {
                    /* Check again every 10 milliseconds: */
                    Thread.Sleep(10);
                }
            }
            else
            {
                WaitHandle[] arWaitHandles = new WaitHandle[2];
                arWaitHandles[0] = outAR.AsyncWaitHandle;
                arWaitHandles[1] = errAR.AsyncWaitHandle;

                if (!WaitHandle.WaitAll(arWaitHandles))
                {
                    throw new CommandLineException(
                        String.Format("Command line aborted: {0}", fileName));
                    /* Note: arguments aren't also shown in the 
                     * exception as they might contain privileged 
                     * information (such as passwords).
                     */
                }
            }

            string results = outputStreamAsyncReader.EndInvoke(outAR);
            errorMessage = errorStreamAsyncReader.EndInvoke(errAR);

            /* At this point the process should surely have exited,
             * since both the error and output streams have been fully read.
             * To be paranoid, let's check anyway...
             */
            if (!cmdLineProcess.HasExited)
            {
                cmdLineProcess.WaitForExit();
            }

            return results;
        }


        public string Run(string fileName, string arguments, DataReceivedEventHandler logHandler,DataReceivedEventHandler errorHandler)
        {
            string result;
            string errorMsg = String.Empty;

            result = Run(fileName, arguments, out errorMsg, logHandler, errorHandler);

            if (errorMsg.Length > 0)
                throw new CommandLineException(errorMsg);

            return result;
        }

        public string Run(string fileName, DataReceivedEventHandler logHandler,DataReceivedEventHandler errorHandler)
        {
            return Run(fileName, "", logHandler,errorHandler);
        }
    }
}
