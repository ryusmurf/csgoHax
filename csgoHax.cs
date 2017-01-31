using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace csgoHax
{
    class GerenciaMemoria
    {
        // Important dlls
        [DllImport("kernel32.dll")]public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref IntPtr lpNumberOfBytesRead);
        [DllImport("kernel32.dll")]public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, int lpNumberOfBytesWritten);
        [DllImport("kernel32.dll")]public static extern bool CloseHandle(int hObject);
        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]public static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);

        // Variables declaration
        Process[] MeuProcesso;

        private IntPtr controladorProcesso = IntPtr.Zero;

        public IntPtr endBaseCliente = IntPtr.Zero;
        public int tamModuloCliente;
        public IntPtr endBaseMotor = IntPtr.Zero;
        public int tamBaseMotor;

        // Search for csgo.exe
        public bool Inicia(string nomeProcesso = "csgo", string nomeJanela = "Counter-Strike: Global Offensive")
        {
            MeuProcesso = Process.GetProcessesByName(nomeProcesso);

            if (nomeProcesso == "")
                return false;

            if (MeuProcesso == null || MeuProcesso.Length == 0)
                return false;

            if ((controladorProcesso = OpenProcess(2035711, false, MeuProcesso[0].Id)) == IntPtr.Zero)
                return false;

            if (FindWindowByCaption(IntPtr.Zero, nomeJanela) == IntPtr.Zero)
                return false;

            if ((endBaseCliente = EndImagemDll("client.dll", out tamModuloCliente)) == IntPtr.Zero)
                return false;

            if ((endBaseMotor = EndImagemDll("engine.dll", out tamBaseMotor)) == IntPtr.Zero)
                return false;

            return true;
        }


        public IntPtr EndImagemDll(string nomedll, out int tam)
        {
            ProcessModuleCollection modulos = MeuProcesso[0].Modules;

            foreach (ProcessModule moduloProcesso in modulos)
            {
                if (nomedll == moduloProcesso.ModuleName)
                {
                    tam = moduloProcesso.ModuleMemorySize;
                    return moduloProcesso.BaseAddress;
                }
            }
            tam = 0;
            return IntPtr.Zero;
        }

        public int EncontraPadrao(byte[] padrao, string mask, string modulo)
        {
            int tamModulo = 0;
            IntPtr moduleBase = EndImagemDll(modulo, out tamModulo);
            if (tamModulo == 0)
            {
                string errorMessage = string.Format("Size of module {0} is INVALID.", modulo);
                throw new Exception(errorMessage);
            }

            for (int i = 0; i < tamModulo - mask.Length; i++)
            {
                bool encontrado = true;
                IntPtr numBytes = IntPtr.Zero;
                int tam = mask.Length;
                byte[] buffer = new byte[tam];
                if (ReadProcessMemory(controladorProcesso, moduleBase + i, buffer, tam, ref numBytes))
                    for (int l = 0; l < mask.Length; l++)
                    {
                        encontrado = mask[l] == '?' || buffer[l] == padrao[l];
                        if (!encontrado)
                            break;
                    }

                if (encontrado)
                    return i;
            }
            return 0;
        }

        public int EncontraPadrao(byte[] padrao, string mask, IntPtr baseModulo, int tamModulo)
        {
            if (tamModulo == 0)
            {
                string errorMessage = string.Format("Size of module is INVALID.");
                throw new Exception(errorMessage);
            }

            for (int i = 0; i < tamModulo - mask.Length; i++)
            {
                bool encontrado = true;
                IntPtr numBytes = IntPtr.Zero;
                int tam = mask.Length;
                byte[] buffer = new byte[tam];
                if (ReadProcessMemory(controladorProcesso, baseModulo + i, buffer, tam, ref numBytes))
                    for (int l = 0; l < mask.Length; l++)
                    {
                        encontrado = mask[l] == '?' || buffer[l] == padrao[l];
                        if (!encontrado)
                            break;
                    }

                if (encontrado)
                    return i;
            }
            return 0;
        }

        public T Read<T>(IntPtr end)
        {
            IntPtr numBytes = IntPtr.Zero;
            int tam = Marshal.SizeOf(typeof(T));
            byte[] buffer = new byte[tam];
            if (ReadProcessMemory(controladorProcesso, end, buffer, tam, ref numBytes))
                return BytesToT<T>(buffer);

            return default(T);
        }

        public T BytesToT<T>(byte[] data, T defVal = default(T))
        {
            T estrutura = defVal;
            GCHandle gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            estrutura = (T)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(T));
            gcHandle.Free();

            return estrutura;
        }

        public void Write<T>(IntPtr end, T toWrite = default(T)) where T : struct
        {
            int tam = Marshal.SizeOf(typeof(T));
            WriteProcessMemory(controladorProcesso, end, TToBytes(toWrite), tam, 0);

            return;
        }

        public byte[] TToBytes<T>(T value) where T : struct
        {
            int tam = Marshal.SizeOf(typeof(T));
            byte[] data = new byte[tam];

            IntPtr ptr = Marshal.AllocHGlobal(tam);
            Marshal.StructureToPtr(value, ptr, true);
            Marshal.Copy(ptr, data, 0, tam);
            Marshal.FreeHGlobal(ptr);

            return data;
        }
    }

    class Offsets
    {
        // Important offsets for the hack
        public static int LocalPlayer = 0;
        public static int ObjectBase = 0;
        public static int EntityList = 0;

        public static class CSPlayer
        {
            public static int health = 0xFC;
            public static int teamNum = 0xF0;
            public static int m_bDormant = 0xE9;
            public static int bSpotted = 0x939;
            public static int index = 0x64;
            public static int glowIndex = 0xA320;
        }

        public static void AtualizaOffsets(GerenciaMemoria gMem)
        {
            GetPlayerLocal(gMem);
            GetBaseObjeto(gMem);
            GetListaEntidades(gMem);
        }

        static void GetPlayerLocal(GerenciaMemoria gMem)
        {
            int end;
            end = gMem.EncontraPadrao(new byte[] { 0xFC, 0xE8, 0x00, 0x00, 0x00, 0x00, 0x8B, 0x3D }, "xx????xx", gMem.endBaseCliente, gMem.tamModuloCliente) + 8;
            LocalPlayer = gMem.Read<int>(gMem.endBaseCliente + end) - gMem.endBaseCliente.ToInt32();
        }

        static void GetBaseObjeto(GerenciaMemoria gMem)
        {
            int end;
            end = gMem.EncontraPadrao(new byte[] { 0xE8, 0x0, 0x0, 0x0, 0x0, 0x83, 0xC4, 0x04, 0xB8, 0x0, 0x0, 0x0, 0x0, 0xC3, 0xcc }, "x????xxxx????xx", gMem.endBaseCliente, gMem.tamModuloCliente) + 9;
            ObjectBase = gMem.Read<int>(gMem.endBaseCliente + end) - gMem.endBaseCliente.ToInt32();
        }

        static void GetListaEntidades(GerenciaMemoria gMem)
        {
            int end;
            end = gMem.EncontraPadrao(new byte[] { 0xBB, 0x00, 0x00, 0x00, 0x00, 0x83, 0xFF, 0x01, 0x0F, 0x8C, 0x00, 0x00, 0x00, 0x00, 0x3B, 0xF8 }, "x????xxxxx????xx", gMem.endBaseCliente, gMem.tamModuloCliente) + 1;
            EntityList = gMem.Read<int>(gMem.endBaseCliente + end) - gMem.endBaseCliente.ToInt32();
        }
    }

    class Player
    {
        public IntPtr baseAddr;
        public int index;
        public int vida;
        public int time;
        public bool dormente;
        public bool estaVivo;
        public int glowIndex;

        public Player() { }

        // Gives you offsets information
        public Player(GerenciaMemoria gMem, int index)
        {
            baseAddr = (IntPtr)gMem.Read<uint>(gMem.endBaseCliente + Offsets.EntityList + ((index - 1) * 16));
            this.index = index;
            vida = gMem.Read<int>(baseAddr + Offsets.CSPlayer.health);
            glowIndex = gMem.Read<int>(baseAddr + Offsets.CSPlayer.glowIndex);
            time = gMem.Read<int>(baseAddr + Offsets.CSPlayer.teamNum);
            dormente = gMem.Read<bool>(baseAddr + Offsets.CSPlayer.m_bDormant);
            estaVivo = vida > 0;
        }
    }

    class LocalPlayer : Player
    {
        public LocalPlayer(GerenciaMemoria gMem)
        {
            baseAddr = (IntPtr)gMem.Read<uint>(gMem.endBaseCliente + Offsets.LocalPlayer);
            index = gMem.Read<int>(baseAddr + Offsets.CSPlayer.index);
            time = gMem.Read<int>(baseAddr + Offsets.CSPlayer.teamNum);
        }
    }

    class ListaEntidades
    {
        public LocalPlayer local;
        public List<Player> jogadores = new List<Player>();

        // Check the number of players in the match in a maximum of 64
        public ListaEntidades(GerenciaMemoria gMem)
        {
            local = new LocalPlayer(gMem);
            for (int i = 0; i < 64; i++)
            {
                if (i != local.index)
                    jogadores.Add(new Player(gMem, i));
            }
        }

        public Player GetPlayer(GerenciaMemoria gMem, int index)
        {
            return new Player(gMem, index);
        }
    }

    class Wallhack
    {
        // Color cointer
        IntPtr glowObj;

        public Wallhack(IntPtr glowObj)
        {
            this.glowObj = glowObj;
        }

        // A function that adds a color around the players, being visible through the wall
        public void SetGlow(Player jogador, GerenciaMemoria gMem, Color color)
        {
            gMem.Write(glowObj + (jogador.glowIndex * 0x38 + 0x24), 1);
            gMem.Write(glowObj + (jogador.glowIndex * 0x38 + 0x25), 0);
            gMem.Write(glowObj + (jogador.glowIndex * 0x38 + 0x26), 0);
            gMem.Write(glowObj + (jogador.glowIndex * 0x38 + 0x4), color);
        }

        // Radar hack, function displays the enemy player on the radar of the game itself
        public void SetSpotted(Player player, GerenciaMemoria Mem)
        {
            Mem.Write(player.baseAddr + Offsets.CSPlayer.bSpotted, true);
        }
    }

    struct Color
    {
        float r, g, b, a;
        public Color(float r, float g, float b)
        {
            this.r = r / 255;
            this.g = g / 255;
            this.b = b / 255;

            // Changes the glow opacity of both teams
            a = 0.6f;
        }
    }

    class Program
    {
        // Dll to check keyboard keys to turn hack on or off
        [DllImport("user32.dll")]public static extern short GetAsyncKeyState(int vKey);

        static void Main(string[] args)
        {
            GerenciaMemoria gMem;
            bool radarOn = true, espOn = true;

            Console.WriteLine("Esperando abrir csgo!");
            Console.Title = "csgoHAX radar + wallhack by Drão";

            // Repeat structure waiting for the game to be opened
            do
            {
                gMem = new GerenciaMemoria();

            } while (!gMem.Inicia());

            Console.WriteLine("Jogo encontrado!");
            Console.WriteLine(">Atualizando Hack!");

            // Update the offsets to not write in memory in the wrong places and take untrusted
            Offsets.AtualizaOffsets(gMem);

            Console.WriteLine("Jogador Local: " + Offsets.LocalPlayer.ToString("X"));
            Console.WriteLine("Base do Objeto: " + Offsets.ObjectBase.ToString("X"));
            Console.WriteLine("Lista de Entidades: " + Offsets.EntityList.ToString("X"));
            Console.WriteLine("\n>HACK ATIVADO!");
            Console.WriteLine("\nF6 Liga/Desliga Wallhack");
            Console.WriteLine("F7 Liga/Desliga radar HACK\n");

            // Repeat structure is always waiting for writing in memory, waiting for keys to be pressed, updating radar, changing glow color depending on the life, etc.
            while (true)
            {
                // Checks if the F7 key has been pressed and changes the value of the bool variable
                if (Convert.ToBoolean(GetAsyncKeyState(0x76) & 1))
                {
                    radarOn = !radarOn;
                    Console.WriteLine("Radar: " + radarOn);
                }

                // Checks if the F6 key has been pressed and changes the value of the bool variable
                if (Convert.ToBoolean(GetAsyncKeyState(0x75) & 1))
                {
                    espOn = !espOn;
                    Console.WriteLine("Wallhack: " + espOn);
                }

                ListaEntidades entList = new ListaEntidades(gMem);
                Wallhack wh = new Wallhack((IntPtr)gMem.Read<uint>(gMem.endBaseCliente + Offsets.ObjectBase));

                foreach (var jogador in entList.jogadores)
                {
                    // If player is alive and dormant (dead player) is alive)
                    if (jogador.estaVivo && !jogador.dormente)
                    {
                        // If player is of the enemy team
                        if (jogador.time != entList.local.time)
                        {
                            // The displays on the radar
                            if (radarOn)
                                wh.SetSpotted(jogador, gMem);

                            // Changes the color of the glow of the enemy team, depending on the life, 100 HP equal to green
                            if (espOn)
                                wh.SetGlow(jogador, gMem, new Color(255 - jogador.vida * 2.55f, jogador.vida * 2.55f, 0));
                        }
                        else
                        {
                            /* Changes the glow color of the own team
                            if (espOn)
                                wh.SetGlow(jogador, gMem, new Color(0, 0, 255.0f));
                            */
                        }
                    }
                }
                // Sleep to avoid overloading the processor and loss of fps
                Thread.Sleep(1);
            }
        }
    }
}
