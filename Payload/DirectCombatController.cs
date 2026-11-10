// Witcher Direct Combat beta controller bridge. Compile with the Windows .NET Framework C# compiler.
// Copyright (c) 2026 Witcher Direct Combat contributors. MIT license; see LICENSE.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class DirectCombatController
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftX;
        public short LeftY;
        public short RightX;
        public short RightY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint Packet;
        public Gamepad Pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public UIntPtr Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint Data;
        public uint Flags;
        public uint Time;
        public UIntPtr Extra;
    }

    // This build targets 64-bit Windows. SendInput's union begins at byte 8 there.
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public KeyInput Key;
        [FieldOffset(8)] public MouseInput Mouse;
    }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint slot, out XInputState state);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int max);

    private const ushort A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
    private const ushort Menu = 0x0010;
    private const ushort DUp = 0x0001, DDown = 0x0002, DLeft = 0x0004, DRight = 0x0008;
    private const ushort LB = 0x0100, RB = 0x0200;
    private const uint ScanCode = 0x0008, KeyUp = 0x0002;
    private const uint MouseMove = 0x0001, LeftDown = 0x0002, LeftUp = 0x0004;
    private const uint RightDown = 0x0008, RightUp = 0x0010;
    private const int StickEngage = 12000, StickRelease = 8500, CameraDeadzone = 7000;
    private const int TriggerThreshold = 96;
    private const ushort OverShoulderCamera = 0x3D; // F3
    private const ushort PauseGame = 0x39; // Space
    private static readonly Dictionary<char, ushort> MovementScans = new Dictionary<char, ushort>
    {
        { 'W', 0x11 }, { 'A', 0x1E }, { 'S', 0x1F }, { 'D', 0x20 }
    };

    private static void Send(Input input)
    {
        if (SendInput(1, new Input[] { input }, Marshal.SizeOf(typeof(Input))) != 1)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private static void Key(ushort scan, bool down)
    {
        Input input = new Input();
        input.Type = 1;
        input.Key = new KeyInput { Scan = scan, Flags = ScanCode | (down ? 0u : KeyUp) };
        Send(input);
    }

    private static void Tap(ushort scan)
    {
        Key(scan, true);
        try { Thread.Sleep(50); }
        finally { Key(scan, false); }
    }

    private static void Mouse(uint flags, int dx, int dy)
    {
        Input input = new Input();
        input.Type = 0;
        input.Mouse = new MouseInput { Dx = dx, Dy = dy, Flags = flags };
        Send(input);
    }

    private static void Click()
    {
        Mouse(LeftDown, 0, 0);
        Mouse(LeftUp, 0, 0);
    }

    private static string ForegroundTitle()
    {
        StringBuilder text = new StringBuilder(256);
        GetWindowText(GetForegroundWindow(), text, text.Capacity);
        return text.ToString();
    }

    private static bool Down(ushort buttons, ushort mask) { return (buttons & mask) != 0; }

    private static double CameraAxis(short value)
    {
        int magnitude = Math.Abs((int)value);
        if (magnitude <= CameraDeadzone) return 0;
        double fraction = (double)(magnitude - CameraDeadzone) / (32767 - CameraDeadzone);
        return Math.Sign(value) * fraction * fraction;
    }

    private static HashSet<char> WantedMovement(Gamepad pad, HashSet<char> held)
    {
        HashSet<char> wanted = new HashSet<char>();
        if (pad.LeftY >= (held.Contains('W') ? StickRelease : StickEngage)) wanted.Add('W');
        else if (pad.LeftY <= -(held.Contains('S') ? StickRelease : StickEngage)) wanted.Add('S');
        if (pad.LeftX >= (held.Contains('D') ? StickRelease : StickEngage)) wanted.Add('D');
        else if (pad.LeftX <= -(held.Contains('A') ? StickRelease : StickEngage)) wanted.Add('A');
        return wanted;
    }

    private static char DodgeDirection(Gamepad pad)
    {
        if (Math.Max(Math.Abs((int)pad.LeftX), Math.Abs((int)pad.LeftY)) < StickRelease) return 'S';
        if (Math.Abs((int)pad.LeftX) > Math.Abs((int)pad.LeftY)) return pad.LeftX > 0 ? 'D' : 'A';
        return pad.LeftY > 0 ? 'W' : 'S';
    }

    private static void Dodge(char direction)
    {
        ushort scan = MovementScans[direction];
        Key(scan, true); Thread.Sleep(60); Key(scan, false);
        Thread.Sleep(60);
        Key(scan, true);
        try { Thread.Sleep(60); }
        finally { Key(scan, false); }
    }

    private static void Attack(ushort styleScan)
    {
        Tap(styleScan);
        Thread.Sleep(50);
        Click();
    }

    private static void SelectSign(ushort scan)
    {
        Tap(scan);
    }

    private static void ReleaseHeld(HashSet<char> held, ref bool signMouseDown)
    {
        foreach (char direction in held) Key(MovementScans[direction], false);
        held.Clear();
        if (signMouseDown) { Mouse(RightUp, 0, 0); signMouseDown = false; }
    }

    private static Process FindWitcherProcess()
    {
        Process[] games = Process.GetProcessesByName("witcher");
        if (games.Length == 0) return null;
        for (int i = 1; i < games.Length; i++) games[i].Dispose();
        return games[0];
    }

    private static int Run(string[] args)
    {
        if (IntPtr.Size != 8)
        {
            Console.Error.WriteLine("This beta requires 64-bit Windows.");
            return 1;
        }
        int slot = 0;
        int duration = 86400;
        int startupTimeout = 30;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--slot") int.TryParse(args[i + 1], out slot);
            if (args[i] == "--duration") int.TryParse(args[i + 1], out duration);
            if (args[i] == "--startup-timeout") int.TryParse(args[i + 1], out startupTimeout);
        }
        startupTimeout = Math.Max(1, startupTimeout);
        Console.WriteLine("Witcher Direct Combat: controller slot {0}; press Ctrl+C to stop.", slot);
        Stopwatch clock = Stopwatch.StartNew();
        double deadline = duration;
        double previousTick = clock.Elapsed.TotalSeconds;
        double quickCastUntil = 0;
        double remainderX = 0, remainderY = 0;
        bool previousX = false, previousY = false, previousA = false, previousB = false;
        bool previousMenu = false;
        bool previousLT = false, previousSignCast = false;
        bool f3Selected = false;
        ushort previousSignButtons = 0;
        bool signMouseDown = false, nextSteel = true;
        Process witcherProcess = null;
        double nextStartupCheck = 0, lastControllerInput = 0, nextIdleCheck = 30;
        bool wasIdle = false;
        HashSet<char> held = new HashSet<char>();
        try
        {
            while (clock.Elapsed.TotalSeconds < deadline || quickCastUntil > clock.Elapsed.TotalSeconds)
            {
                double now = clock.Elapsed.TotalSeconds;
                if (witcherProcess == null && now >= nextStartupCheck)
                {
                    witcherProcess = FindWitcherProcess();
                    nextStartupCheck = now + 1.0;
                }
                if (witcherProcess == null && now >= startupTimeout)
                {
                    Console.WriteLine("Witcher did not start within the timeout; stopping controller bridge.");
                    break;
                }
                double dt = Math.Min(now - previousTick, 0.05);
                previousTick = now;
                XInputState state;
                bool connected = XInputGetState((uint)slot, out state) == 0;
                Gamepad pad = connected ? state.Pad : new Gamepad();
                bool controllerInput = connected && (pad.Buttons != 0 ||
                    pad.LeftTrigger >= 10 || pad.RightTrigger >= 10 ||
                    Math.Abs((int)pad.LeftX) >= StickRelease || Math.Abs((int)pad.LeftY) >= StickRelease ||
                    Math.Abs((int)pad.RightX) > CameraDeadzone || Math.Abs((int)pad.RightY) > CameraDeadzone);
                if (controllerInput)
                {
                    if (wasIdle && witcherProcess != null && witcherProcess.HasExited)
                    {
                        Console.WriteLine("Witcher closed; stopping controller bridge.");
                        break;
                    }
                    lastControllerInput = now;
                    wasIdle = false;
                }
                else if (witcherProcess != null && now - lastControllerInput >= 30 && now >= nextIdleCheck)
                {
                    if (witcherProcess.HasExited)
                    {
                        Console.WriteLine("Witcher closed; stopping controller bridge.");
                        break;
                    }
                    nextIdleCheck = now + 30;
                    wasIdle = true;
                }
                bool active = connected && witcherProcess != null && ForegroundTitle().StartsWith("The Witcher (");
                HashSet<char> wanted = active ? WantedMovement(pad, held) : new HashSet<char>();
                foreach (char key in held) if (!wanted.Contains(key)) Key(MovementScans[key], false);
                foreach (char key in wanted) if (!held.Contains(key)) Key(MovementScans[key], true);
                held = wanted;

                double cameraX = active ? CameraAxis(pad.RightX) : 0;
                double cameraY = active ? -CameraAxis(pad.RightY) : 0;
                bool cameraStick = cameraX != 0 || cameraY != 0;
                if (active && !f3Selected)
                {
                    Tap(OverShoulderCamera);
                    Console.WriteLine("Selected F3 over-the-shoulder camera.");
                    f3Selected = true;
                }
                if (cameraStick)
                {
                    double rawX = cameraX * 900 * dt + remainderX;
                    double rawY = cameraY * 900 * dt + remainderY;
                    int dx = (int)rawX, dy = (int)rawY;
                    remainderX = rawX - dx; remainderY = rawY - dy;
                    if (dx != 0 || dy != 0) Mouse(MouseMove, dx, dy);
                }
                else { remainderX = 0; remainderY = 0; }

                bool x = connected && Down(pad.Buttons, X);
                bool y = connected && Down(pad.Buttons, Y);
                bool a = connected && Down(pad.Buttons, A);
                bool b = connected && Down(pad.Buttons, B);
                bool menu = connected && Down(pad.Buttons, Menu);
                if (active && menu && !previousMenu) Tap(PauseGame);
                previousMenu = menu;
                if (active && x && !previousX) Attack(0x2C); // Z: Strong
                if (active && y && !previousY) Attack(0x2E); // C: Group
                if (active && a && !previousA) Attack(0x2D); // X: Fast
                previousX = x; previousY = y; previousA = a;

                bool lt = connected && pad.LeftTrigger >= TriggerThreshold;
                if (active && lt && !previousLT)
                {
                    Tap(nextSteel ? (ushort)0x10 : (ushort)0x13); // Q: steel, R: extra weapon 1
                    nextSteel = !nextSteel;
                }
                previousLT = lt;

                ushort signButtons = connected ? pad.Buttons : (ushort)0;
                if (active)
                {
                    if (Down(signButtons, DUp) && !Down(previousSignButtons, DUp))
                    {
                        SelectSign(0x02); // 1: Aard
                        Thread.Sleep(50);
                        if (!signMouseDown) { Mouse(RightDown, 0, 0); signMouseDown = true; }
                        quickCastUntil = clock.Elapsed.TotalSeconds + 1.0;
                    }
                    if (Down(signButtons, DRight) && !Down(previousSignButtons, DRight)) SelectSign(0x05); // 4: Igni
                    if (Down(signButtons, DDown) && !Down(previousSignButtons, DDown)) SelectSign(0x04); // 3: Yrden
                    if (Down(signButtons, DLeft) && !Down(previousSignButtons, DLeft)) SelectSign(0x03); // 2: Quen
                    if (Down(signButtons, LB) && !Down(previousSignButtons, LB)) SelectSign(0x06); // 5: Axii
                }
                previousSignButtons = signButtons;
                bool signCast = connected && (Down(pad.Buttons, RB) || pad.RightTrigger >= TriggerThreshold);
                if (active && signCast && !previousSignCast && !signMouseDown)
                {
                    Mouse(RightDown, 0, 0);
                    signMouseDown = true;
                }
                if (signMouseDown && (!active || (!signCast && clock.Elapsed.TotalSeconds >= quickCastUntil)))
                {
                    Mouse(RightUp, 0, 0);
                    signMouseDown = false;
                    quickCastUntil = 0;
                }
                previousSignCast = signCast;

                if (active && b && !previousB)
                {
                    char direction = DodgeDirection(pad);
                    foreach (char key in held) Key(MovementScans[key], false);
                    held.Clear();
                    Dodge(direction);
                }
                previousB = b;
                Thread.Sleep(20);
            }
        }
        finally
        {
            ReleaseHeld(held, ref signMouseDown);
            if (witcherProcess != null) witcherProcess.Dispose();
        }
        Console.WriteLine("Controller bridge stopped; held input released.");
        return 0;
    }

    private static int Main(string[] args)
    {
        bool firstInstance;
        Mutex oneBridge = new Mutex(true, "Local\\WitcherDirectCombatController", out firstInstance);
        if (!firstInstance)
        {
            Console.WriteLine("Witcher Direct Combat is already running.");
            oneBridge.Dispose();
            return 0;
        }
        try { return Run(args); }
        finally
        {
            oneBridge.ReleaseMutex();
            oneBridge.Dispose();
        }
    }
}
