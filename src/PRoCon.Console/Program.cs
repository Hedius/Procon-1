﻿/*  Copyright 2010 Geoffrey 'Phogue' Green

    http://www.phogue.net

    This file is part of PRoCon Frostbite.

    PRoCon Frostbite is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    PRoCon Frostbite is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with PRoCon Frostbite.  If not, see <http://www.gnu.org/licenses/>.
 */


using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using PRoCon.Core;
using PRoCon.Core.Remote;

namespace PRoCon.Console {
    class Program {
        static void Main(string[] args) {
            int connectionIntrupts = 0;

            int maxConnectionIntruppts = 5;

            int iValue;

            if (args != null && args.Length >= 2) {
                for (int i = 0; i < args.Length; i = i + 2) {
                    if (String.Compare("-use_core", args[i], true) == 0 && int.TryParse(args[i + 1], out iValue) == true && iValue > 0) {
                        Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)iValue;
                    }
                }
            }

            PRoConApplication application = null;

            if (PRoConApplication.IsProcessOpen() == false) {
                try {
                    application = new PRoConApplication(true, args);

                    System.Console.WriteLine("Procon Frostbite");
                    System.Console.WriteLine("================");
                    System.Console.WriteLine("By executing this application you agree to the license available at:");
                    System.Console.WriteLine("\thttps://myrcon.net/licenses/myrcon.pdf");
                    System.Console.WriteLine("If you do not agree you must immediately exit this application.");
                    System.Console.WriteLine("================");
                    System.Console.WriteLine("This is a cut down version of PRoCon.exe to be used by GSPs and PRoCon Hosts.");
                    System.Console.WriteLine("Executing this file is the same as \"PRoCon.exe -console 1\"");
                    System.Console.WriteLine("No output is given.  This is as cut down as we're gunno get..");
                    System.Console.WriteLine("\nExecuting procon...");
                    application.Execute();

                    GC.Collect();

                    // Check if we are running in a docker container
                    if (File.Exists("/proc/1/cgroup") == true) {
                        string strCGroup = File.ReadAllText("/proc/1/cgroup");
                        if (strCGroup.Contains("/docker/") == true) {
                            System.Console.WriteLine("[PRoCon] Running in a Docker container.");
                        }
                    }

                    // Check if the environemnt variable "PROCON_GAMESERVER_IP" exists
                    string PROCON_GAMESERVER_IP = Environment.GetEnvironmentVariable("PROCON_GAMESERVER_IP") ?? "";

                    if (PROCON_GAMESERVER_IP != "") {
                        // Run a background thread to keep checking if the connection is still alive, otherwise close application.
                        Thread t = new Thread(new ThreadStart(delegate {
                            Int32.TryParse(Environment.GetEnvironmentVariable("PROCON_GAMESERVER_PORT"), out int PROCON_GAMESERVER_PORT);

                            while (true) {
                                string currentTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                                System.Console.WriteLine("[" + currentTimestamp + "] [PRoCon] [Watchdog] Testing connection to game server...");
                                Thread.Sleep(5000);

                                // Check if port is alive using the ip PROCON_GAMESERVER_IP and port PROCON_GAMESERVER_PORT
                                using (TcpClient tcpClient = new TcpClient()) {
                                    try {
                                        // Reset the connectionIntrupts upponse successful connect.
                                        if (connectionIntrupts > 0) {
                                            connectionIntrupts = 0;
                                        }

                                        tcpClient.Connect(PROCON_GAMESERVER_IP, PROCON_GAMESERVER_PORT);

                                        // If we get here, the connection is alive.
                                        // Now lets clean up the tcpClient by closing the connection
                                        tcpClient.Close();

                                        System.Console.WriteLine("[" + currentTimestamp + "] [PRoCon] [Watchdog] Game server connection successful.");
                                    }
                                    catch (Exception) {
                                        // Once we reach the max amount of connection attempts, kill the application.
                                        if (connectionIntrupts > maxConnectionIntruppts) {
                                            System.Console.WriteLine("[" + currentTimestamp + "] [PRoCon] [Watchdog] Connection to game server lost, closing application.");
                                            application.Shutdown();
                                            // Exit the application
                                            Environment.Exit(1);
                                            break;
                                        }
                                        else {
                                            connectionIntrupts++;
                                        }
                                    }
                                }
                            }
                        }));

                        t.Start();
                    }

                    System.Console.WriteLine("Running... (Press any key to shutdown)");
                    System.Console.ReadKey();
                }
                catch (Exception e) {
                    FrostbiteConnection.LogError("PRoCon.Console.exe", "", e);
                }
                finally {
                    if (application != null) {
                        application.Shutdown();
                    }
                }
            }
            else {
                // Possible prevention of a cpu consumption bug I can see at the time of writing.
                // TCAdmin: Start procon.exe
                // procon.exe has an update to install
                // procon.exe loads proconupdater.exe
                // procon.exe unloads
                // proconupdater.exe begins update
                // TCAdmin detects procon.exe shutdown - attempts to reload
                System.Console.WriteLine("Already running - shutting down");
                Thread.Sleep(50);
            }
        }
    }
}