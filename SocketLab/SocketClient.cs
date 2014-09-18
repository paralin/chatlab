using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketLab
{
    public class SocketClient
    {
        /// <summary>
        /// ID of the client
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Nick of the client
        /// </summary>
        public string Nick { get; set; }

        /// <summary>
        /// Socket
        /// </summary>
        public Socket Socket { get; set; }

        /// <summary>
        /// Stream
        /// </summary>
        public Stream Stream { get; set; }

        /// <summary>
        /// Reader
        /// </summary>
        public StreamReader Reader { get; set; }

        /// <summary>
        /// Writer
        /// </summary>
        public StreamWriter Writer { get; set; }

        /// <summary>
        /// Admin of room
        /// </summary>
        public bool Admin { get; set; }

        /// <summary>
        /// Name of the room lowercase
        /// </summary>
        public string Room { get; set; }

        public void StartThread()
        {
            ThreadPool.QueueUserWorkItem(ProcThread);
        }

        private void ProcThread(object st)
        {
            Socket.Blocking = true;
            bool run = true;
            try
            {
                Writer.WriteLine("Your ID: " + Id);
                foreach (var cli in Program.dict.Values)
                {
                    Writer.WriteLine("CLIENT: " + cli.Nick);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Issue sending startup sequence to "+Id);
            }
            while (Socket.Connected && run)
            {
                try
                {
                    string line = Reader.ReadLine();
                    if (line == null) run = false;
                    else
                    {
                        if (line.StartsWith("/") && line.Length > 2)
                        {
                            var parts = line.Substring(1).Replace("\n", "").Split(' ');
                            switch (parts[0])
                            {
                                case "id":
                                    if (parts.Length != 2) Writer.WriteLine("Usage: /id nickname");
                                    else
                                    {
                                        if (Program.dict.Values.Any(m => m.Nick.ToLower() == parts[1]))
                                        {
                                            Writer.WriteLine("Nickname is already in use.");
                                        }
                                        else
                                        {
                                            foreach (var cli in Program.dict.Values)
                                            {
                                                try
                                                {
                                                    cli.Writer.WriteLine(Nick + " -> " + parts[1]);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine("Problem transmitting nick change to " + cli.Id);
                                                }
                                            }
                                            Nick = parts[1];
                                        }
                                    }
                                    break;
                                case "whisper":
                                    if (parts.Length < 3)
                                    {
                                        Writer.WriteLine("Usage: /whisper othernick Message here.");
                                        break;
                                    }
                                    var msg = string.Join(" ", parts.Skip(2).ToArray());
                                    var other = parts[1].ToLower();
                                    if (other == Nick.ToLower()) Writer.WriteLine("You can't whisper to yourself!");
                                    else
                                    {
                                        var cli = Program.dict.Values.FirstOrDefault(m => m.Nick.ToLower() == other);
                                        if (cli == null) Writer.WriteLine("Can't find " + other + ".");
                                        else
                                        {
                                            Writer.WriteLine("-> "+other+": "+msg);
                                            cli.Writer.WriteLine("[whisper] "+Nick+": "+msg);
                                            Console.WriteLine("[whisper] -> "+other+" " + Nick + ": " + msg);
                                        }
                                    }
                                    break;
                                case "part":
                                    LeaveRoom();
                                    break;
                                case "join":
                                    if (parts.Length != 2) Writer.WriteLine("Usage: /join roomname");
                                    else
                                    {
                                        if (Room == parts[1])
                                        {
                                            Writer.WriteLine("You are already in that room.");
                                            break;
                                        }
                                        LeaveRoom();
                                        Room = parts[1].ToLower();
                                        Writer.WriteLine("You have joined "+Room+".\nMEMBERS:");
                                        foreach (var cli in Program.dict.Values.Where(m=>m.Room==Room))
                                        {
                                            Writer.WriteLine("MEMBER: " + cli.Nick);
                                        }
                                        var others = Program.dict.Where(m => m.Value != this && m.Value.Room == Room);
                                        foreach (var cli in others)
                                        {
                                            try
                                            {
                                                cli.Value.Writer.WriteLine(Nick + " joined the room.");
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine("Issue transmitting join message to " + cli.Key + " " + ex);
                                            }
                                        }
                                        if (!others.Any())
                                        {
                                            Writer.WriteLine("You are the first member and are an admin.");
                                            Admin = true;
                                        }
                                    }
                                    break;
                                case "+admin":
                                    if (parts.Length != 2)
                                    {
                                        Writer.WriteLine("Usage: /+admin username");
                                        break;
                                    }
                                    if (!Admin || Room == null)
                                    {
                                        Writer.WriteLine("You are not in a room or not an admin.");
                                        break;
                                    }
                                    var toAdd = Program.dict.Values.FirstOrDefault(m => m.Nick.ToLower() == parts[1].ToLower() && m.Room == Room);
                                    if (toAdd == null)
                                    {
                                        Writer.WriteLine("Can't find that person, or they are not in your room.");
                                        break;
                                    }
                                    if (toAdd.Admin)
                                    {
                                        Writer.WriteLine("That user is already an admin.");
                                        break;
                                    }
                                    toAdd.Admin = true;
                                    Writer.WriteLine("Added " + toAdd.Nick + " as an admin.");
                                    toAdd.Writer.WriteLine("You have been added as an admin of "+Room+" by "+Nick+".");
                                    break;
                                case "-admin":
                                    if (parts.Length != 2)
                                    {
                                        Writer.WriteLine("Usage: /-admin username");
                                        break;
                                    }
                                    if (!Admin || Room == null)
                                    {
                                        Writer.WriteLine("You are not in a room or not an admin.");
                                        break;
                                    }
                                    var toRm = Program.dict.Values.FirstOrDefault(m => m.Nick.ToLower() == parts[1].ToLower() && m.Room == Room);
                                    if (toRm == null)
                                    {
                                        Writer.WriteLine("Can't find that person, or they are not in your room.");
                                        break;
                                    }
                                    if (!toRm.Admin)
                                    {
                                        Writer.WriteLine("That user is not an admin.");
                                        break;
                                    }
                                    toRm.Admin = false;
                                    Writer.WriteLine("Removed " + toRm.Nick + " as an admin.");
                                    toRm.Writer.WriteLine("You have been removed as an admin of " + Room + " by " + Nick + ".");
                                    break;
                                case "kick":
                                    if (parts.Length != 2)
                                    {
                                        Writer.WriteLine("Usage: /kick username");
                                        break;
                                    }
                                    if (!Admin || Room == null)
                                    {
                                        Writer.WriteLine("You are not in a room or not an admin.");
                                        break;
                                    }
                                    var toKick = Program.dict.Values.FirstOrDefault(m => m.Nick.ToLower() == parts[1].ToLower() && m.Room == Room);
                                    if (toKick == null)
                                    {
                                        Writer.WriteLine("Can't find that person, or they are not in your room.");
                                        break;
                                    }
                                    if (toKick.Admin)
                                    {
                                        Writer.WriteLine("That user is an admin and can't be kicked.");
                                        break;
                                    }
                                    toKick.Room = null;
                                    toKick.Admin = false;
                                    Writer.WriteLine("Kicked " + toKick.Nick + " from the room.");
                                    toKick.Writer.WriteLine("You have been kicked from " + Room + " by " + Nick + ".");
                                    foreach (var cli in Program.dict.Where(m => m.Value != this && m.Value.Room == Room))
                                    {
                                        try
                                        {
                                            cli.Value.Writer.WriteLine(Nick + " KICKED by " + Nick);
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine("Issue transmitting kicked notification to " + cli.Key + " " + ex);
                                        }
                                    }
                                    break;
                                default:
                                    Writer.WriteLine("Unknown command.");
                                    break;
                            }
                        }
                        else
                        {
                            if (Room == null)
                            {
                                Writer.WriteLine("You are not currently in a room, use /join to enter one.");
                            }
                            else
                            {
                                foreach (var cli in Program.dict.Where(m=>m.Value != this && m.Value.Room==Room))
                                {
                                    try
                                    {
                                        cli.Value.Writer.WriteLine(Nick + ": " + line.Replace("\n", ""));
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Issue transmitting message to " + cli.Key + " " + ex);
                                    }
                                }
                            }
                            Console.WriteLine("[" + Room + "] " + Socket.RemoteEndPoint + ": " + line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error processing message: "+ex);
                }
            }
            Console.WriteLine("Disconnected "+Socket.RemoteEndPoint);
            Program.dict.Remove(Id);
            foreach (var sock in Program.dict.Values)
            {
                sock.Writer.WriteLineAsync("Disconnected " + Id);
            }
            try
            {
                Socket.Close();
                Socket.Dispose();
            }
            catch
            {
            }
        }

        public void LeaveRoom()
        {
            Admin = false;
            if (Room == null) return;
            var others = Program.dict.Where(m => m.Value != this && m.Value.Room == Room);
            foreach (var cli in others)
            {
                try
                {
                    cli.Value.Writer.WriteLine(Nick + " left the room.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Issue transmitting left notification to " + cli.Key + " " + ex);
                }
            }
            Writer.WriteLine("You have left the room " + Room + ".");
            Room = null;
        }
    }
}
