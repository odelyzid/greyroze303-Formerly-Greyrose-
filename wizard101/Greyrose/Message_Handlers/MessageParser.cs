using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Greyrose
{
    class MessageParser
    {
        /// <summary>
        /// Reads the input packet (as a byte stream), analyses it and sends it to the appropriate message handler
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static Tuple<Byte[],int,int> Parse(byte[] bytes, int length, ClientSession session)
        {
            if (length <= 0)
                return null;

            bool SessionOpen = session.State >= ConnectionState.SESSION_ESTABLISHED;
            ushort SessionID = session.SessionID;

            Stream ByteStream = new MemoryStream(bytes, 0, length, false);
            using (BinaryReader reader = new BinaryReader(ByteStream))
            {
                string header = reader.ReadByte().ToString("X2");
                header = header + reader.ReadByte().ToString("X2");

                if (header == "0DF0")
                {
                    /*
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ServerLog.WriteLine("KI PACKET");
                    Console.ResetColor();
                    */
                }
                else //If the packet doesn't start with 0DF0 (F00D)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ServerLog.WriteLine("UNKNOWN PACKET TYPE!"); //Inform the user that an unknown packet was received
                    Console.ResetColor();
                    return null; //Cancel this process because we don't know how to parse the packet
                }

                int packetLength = reader.ReadUInt16();
                int isControl = reader.ReadByte();
                int opCode = reader.ReadByte();

                int unknown = reader.ReadUInt16(); //The extra 00 00 bytes that seem to always be 00 00. This just seeks the data, really. I don't care what the data is

                if (unknown != 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    ServerLog.WriteLine("\n\n\n############### UNKNOWN DATA WAS NOT 00 00! OH MY GOD IT FUCKING CHANGED! PANIC! ###############"); //If the unknown data is not 00 00 like it *always* is, print a little panic message, lol
                    Console.ResetColor();
                }

                //Debug output, uncomment to display more packet information
                //ServerLog.WriteLine("Length: {0}", length);
                //ServerLog.WriteLine("isControl: {0}", isControl);
                //ServerLog.WriteLine("opCode: {0}", opCode);
                //ServerLog.WriteLine("undefined: {0}", reader.ReadByte());
                //ServerLog.WriteLine("undefined: {0}", reader.ReadByte());

                if (isControl != 0) //If the message has a control code, parse the control code
                {
                    //ServerLog.WriteLine("CONTROL MESSAGE!");
                    Tuple<byte[],int,int> temptuple = Handlers._0ControlMessages(reader, opCode, packetLength, bytes, length);
                    return temptuple;
                }

                if (isControl == 0 && opCode == 0) //If the message is not a control message, and has no opcode. Parse it like a message
                {
                    uint svcid = reader.ReadByte();
                    ServerLog.WriteLine("Service ID: {0}", svcid);

                    if (svcid == 1)
                    {
                        return Tuple.Create(Handlers._1BaseMessages(reader, SessionOpen, SessionID), 0, 0);
                    }
                    else if (svcid == 2)
                    {
                        return Tuple.Create(Handlers._2ExtendedBaseMessages(reader), 0, 0);
                    }
                    else if (svcid == 5)
                    {
                        return Handlers._5GameMessages(reader, session);
                    }
                    else if (svcid == 7)
                    {
                        return Handlers._7LoginMessages(reader, session);
                    }
                    else if (svcid == 8)
                    {
                        return Tuple.Create(Handlers._8PatchMessages(reader), 0, 0);
                    }
                    else if (svcid == 12)
                    {
                        return Tuple.Create(Handlers._12WizardMessages(reader), 0, 0);
                    }
                    else if (svcid == 52)
                    {
                        return Tuple.Create(Handlers._52QuestMessages(reader), 0, 0);
                    }
                    else
                    {
                        ServerLog.WriteLine("Unhandled SVCID: {0}", svcid);
                    }
                    return null;
                }

                ServerLog.WriteLine("UNHANDLED MESSAGE!");

                string hexbytes = null;

                for (int i = 0; i < Math.Min(length, packetLength + 4); i++)
                {
                    hexbytes = hexbytes + bytes[i].ToString("X2");
                }

                ServerLog.WriteLine(hexbytes);

            }



            return null; //No response to send to the client
        }

    }
}
