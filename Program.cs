using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using System;
using System.IO;
using System.Text.Json;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

record SaveData(int Coins, int Level, int Owned, int Knife);
record Platform(Vector3 Pos, float Width, float Depth);
record GhostFrame(Vector3 Pos, float Yaw, float Time);

class Game : GameWindow
{
    const int Count = 12;
    readonly Random rng = new();
    readonly List<Platform> platforms = new();
    readonly List<GhostFrame> recording = new(), replay = new();
    float attemptTime, ghostTime, botInspectTimer, raceTime, rivalProgress;
    bool race, raceFinished, chatting, prevR, prevT, prevBack, prevSend;
    string chatInput="", chatStatus="OFFLINE";
    readonly List<string> messages=new();
    ChatClient? chatClient;
    int botTarget = 1;
    Vector3 pos = new(0, 1.9f, 0), vel;
    float yaw = -90, pitch, inspect, inspectTime;
    bool grounded = true, auto, shop, prevF, prevB, prevTab, prevEnter, prevLeft, prevRight;
    int coins, level = 1, owned = 1, knife, selected;
    double titleTimer;
    readonly string savePath = Path.Combine(AppContext.BaseDirectory, "save.json");
    readonly string[] names = { "Default", "Ruby", "Emerald", "Gold", "Galaxy" };
    readonly int[] prices = { 0, 50, 120, 250, 500 };
    readonly Vector3[] colors = { new(.75f,.8f,.9f), new(.9f,.16f,.2f), new(.12f,.9f,.4f), new(1f,.75f,.12f), new(.65f,.3f,1f) };
    public Game() : base(GameWindowSettings.Default, new NativeWindowSettings { ClientSize = new Vector2i(1280,720), Title = "Tevrix BunnyHop 3D", API = ContextAPI.OpenGL, APIVersion = new Version(2,1), Profile = ContextProfile.Any, Flags = ContextFlags.Default }) { }
    protected override void OnLoad()
    {
        base.OnLoad(); VSync = VSyncMode.On; CursorState = CursorState.Grabbed;
        GL.ClearColor(.055f,.075f,.12f,1); GL.Enable(EnableCap.DepthTest);
        if (File.Exists(savePath)) try { var s = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(savePath)); if(s != null) { coins=Math.Max(0,s.Coins); level=Math.Max(1,s.Level); owned=s.Owned|1; knife=Math.Clamp(s.Knife,0,4); if((owned & (1<<knife))==0) knife=0; } } catch { }
        Generate();
        chatClient=new ChatClient(line => {lock(messages){messages.Add(line);if(messages.Count>7)messages.RemoveAt(0);}});
        chatClient.Connect("127.0.0.1",27777);
    }
    void Save() { try { File.WriteAllText(savePath,JsonSerializer.Serialize(new SaveData(coins,level,owned,knife))); } catch {} }
    void Generate()
    {
        platforms.Clear(); var r = new Random(level * 7187); float x=0,z=0;
        platforms.Add(new Platform(new Vector3(0,0,0), 9, 9));
        for(int i=1;i<Count;i++) { x += (float)(r.NextDouble()*4-2); z-= (float)(7.3+r.NextDouble()*1.2); platforms.Add(new Platform(new Vector3(x,0,z), 4.8f,4.8f)); }
        pos=new Vector3(0,1.9f,0); vel=Vector3.Zero; yaw=-90; pitch=0; grounded=true; botTarget=1; attemptTime=0; ghostTime=0; recording.Clear();
    }
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if(chatting && e.Unicode >= 32 && e.Unicode < 127 && chatInput.Length < 90)
            chatInput += (char)e.Unicode;
    }
    void ResetRace()
    {
        race=true; raceFinished=false; raceTime=0; rivalProgress=0;
        replay.Clear();Generate();auto=false;
    }
    static bool Press(bool now,ref bool prev) { bool hit=now&&!prev; prev=now; return hit; }
    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e); float dt=Math.Min((float)e.Time,.04f); if(dt<=0)return;
        var k=KeyboardState;
        if(k.IsKeyDown(Keys.Escape)) { if(shop){shop=false;CursorState=CursorState.Grabbed;}else Close(); }
        if(Press(k.IsKeyDown(Keys.T),ref prevT) && !shop && !chatting)
        { chatting=true;CursorState=CursorState.Normal; }
        if(chatting)
        {
            if(Press(k.IsKeyDown(Keys.Backspace),ref prevBack) && chatInput.Length>0) chatInput=chatInput[..^1];
            if(Press(k.IsKeyDown(Keys.Enter),ref prevSend))
            {
                if(chatInput.Trim().Length>0) chatClient?.Send("Player: "+chatInput.Trim());
                chatInput="";chatting=false;CursorState=CursorState.Grabbed;
            }
            if(k.IsKeyDown(Keys.Escape)) {chatting=false;CursorState=CursorState.Grabbed;}
            return;
        }
        if(Press(k.IsKeyDown(Keys.R),ref prevR) && !shop) ResetRace();
        if(Press(k.IsKeyDown(Keys.Tab),ref prevTab)) {shop=!shop;CursorState=shop?CursorState.Normal:CursorState.Grabbed;}
        if(Press(k.IsKeyDown(Keys.B),ref prevB)) { auto=!auto; botTarget=1; }
        if(Press(k.IsKeyDown(Keys.F),ref prevF)) inspectTime=1.0f;
        if(shop)
        {
            if(Press(k.IsKeyDown(Keys.Left),ref prevLeft))selected=(selected+names.Length-1)%names.Length;
            if(Press(k.IsKeyDown(Keys.Right),ref prevRight))selected=(selected+1)%names.Length;
            if(Press(k.IsKeyDown(Keys.Enter),ref prevEnter))
            {
                if((owned&(1<<selected))!=0)knife=selected;
                else if(coins>=prices[selected]) {coins-=prices[selected];owned|=1<<selected;knife=selected;}
                Save();
            }
        }
        else
        {
            var mouse=MouseState.Delta;
            if(!auto) {yaw+=mouse.X*.12f; pitch=Math.Clamp(pitch-mouse.Y*.12f,-85,85);}
            Vector3 forward=new(MathF.Cos(MathHelper.DegreesToRadians(yaw)),0,MathF.Sin(MathHelper.DegreesToRadians(yaw)));
            Vector3 right=new(-forward.Z,0,forward.X);
            Vector3 wish=Vector3.Zero;
            if(auto)
            {
                while(botTarget<Count-1 && pos.Z < platforms[botTarget].Pos.Z+1.3f) botTarget++;
                var dest=platforms[botTarget].Pos;
                Vector3 diff=new(dest.X-pos.X,0,dest.Z-pos.Z);
                if(diff.LengthSquared>0.01f) {wish=Vector3.Normalize(diff);yaw=MathHelper.RadiansToDegrees(MathF.Atan2(wish.Z,wish.X));pitch=0;}
            }
            else
            {
                if(k.IsKeyDown(Keys.W))wish+=forward;
                if(k.IsKeyDown(Keys.S))wish-=forward;
                if(k.IsKeyDown(Keys.D))wish+=right;
                if(k.IsKeyDown(Keys.A))wish-=right;
                if(wish.LengthSquared>1)wish=Vector3.Normalize(wish);
            }
            Vector3 horizontal=new(vel.X,0,vel.Z);
            if(grounded)
            {
                horizontal*=MathF.Max(0,1-5.5f*dt);
                Accelerate(ref horizontal,wish,11,35,dt);
                if(auto||k.IsKeyDown(Keys.Space)) {vel.Y=9.5f;grounded=false;}
            }
            else Accelerate(ref horizontal,wish,12.5f,7.5f,dt);
            vel.X=horizontal.X;vel.Z=horizontal.Z;
            float oldY=pos.Y;
            vel.Y-=19f*dt;
            pos+=vel*dt;
            grounded=false;
            foreach(var p in platforms)
            {
                if(MathF.Abs(pos.X-p.Pos.X)<p.Width/2+.22f && MathF.Abs(pos.Z-p.Pos.Z)<p.Depth/2+.22f && vel.Y<=0 && oldY>=p.Pos.Y+1.8f && pos.Y<=p.Pos.Y+1.8f)
                {pos.Y=p.Pos.Y+1.8f;vel.Y=0;grounded=true;break;}
            }
            if(race && !raceFinished)
            {
                raceTime+=dt;
                // Rival follows the exact same platform sequence at a consistent speed.
                rivalProgress=MathF.Min(Count-1,rivalProgress+dt*0.68f);
                if(rivalProgress>=Count-1) {raceFinished=true;race=false;lock(messages)messages.Add("BOT WON THE RACE");}
            }
            attemptTime+=dt;
            if(recording.Count==0 || attemptTime-recording[^1].Time>=.025f) recording.Add(new GhostFrame(pos,yaw,attemptTime));
            ghostTime+=dt;
            if(auto) { botInspectTimer+=dt; if(botInspectTimer>3.1f) {botInspectTimer=0;inspectTime=1.0f;} }
            if(pos.Y< -12) { replay.Clear();replay.AddRange(recording);recording.Clear();attemptTime=0;ghostTime=0;pos=platforms[0].Pos+new Vector3(0,1.9f,0);vel=Vector3.Zero;botTarget=1;grounded=true; }
            var finish=platforms[^1];
            if(MathF.Abs(pos.X-finish.Pos.X)<1.8f && MathF.Abs(pos.Z-finish.Pos.Z)<1.8f && grounded)
            {
                if(race && !raceFinished) {coins+=35;race=false;raceFinished=true;lock(messages)messages.Add("YOU WON THE RACE +35 COINS");}
                coins+=10+level*5;level++;Save();Generate();
            }
        }
        if(inspectTime>0)inspectTime=MathF.Max(0,inspectTime-dt);
        inspect+=dt* (inspectTime>0?7:0);
        titleTimer+=dt;
        if(titleTimer>.18) {titleTimer=0; Title=shop?$"SHOP | Coins: {coins} | < {names[selected]} > | Price: {prices[selected]} | {((owned&(1<<selected))!=0?"OWNED - Enter to equip":"Enter to buy")} | Tab close":$"Tevrix BHOP | Level {level} | Coins {coins} | Knife: {names[knife]} | Auto: {(auto?"ON":"OFF")} | WASD Mouse Space | B bot | F inspect | Tab shop";}
    }
    static void Accelerate(ref Vector3 v,Vector3 wish,float wishSpeed,float accel,float dt)
    {
        if(wish.LengthSquared<.001f)return;
        float add=wishSpeed-Vector3.Dot(v,wish);
        if(add<=0)return;
        v+=wish*MathF.Min(add,accel*wishSpeed*dt);
    }
    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
        GL.Viewport(0,0,Size.X,Size.Y);
        Matrix4 projection=Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(78),Size.X/(float)Math.Max(1,Size.Y),.05f,400);
        GL.MatrixMode(MatrixMode.Projection);GL.LoadMatrix(ref projection);
        Vector3 direction=new(MathF.Cos(MathHelper.DegreesToRadians(pitch))*MathF.Cos(MathHelper.DegreesToRadians(yaw)),MathF.Sin(MathHelper.DegreesToRadians(pitch)),MathF.Cos(MathHelper.DegreesToRadians(pitch))*MathF.Sin(MathHelper.DegreesToRadians(yaw)));
        Matrix4 view=Matrix4.LookAt(pos,pos+direction,Vector3.UnitY);
        GL.MatrixMode(MatrixMode.Modelview);GL.LoadMatrix(ref view);
        for(int i=0;i<platforms.Count;i++)
        {
            var p=platforms[i];
            Box(p.Pos+new Vector3(0,-.32f,0),new Vector3(p.Width,.64f,p.Depth),i==platforms.Count-1?new Vector3(.1f,.8f,.32f):new Vector3(.15f+.02f*(i%3),.35f,.55f));
            Box(p.Pos+new Vector3(0,.025f,0),new Vector3(p.Width,.05f,p.Depth),i==platforms.Count-1?new Vector3(.3f,1,.4f):new Vector3(.4f,.65f,.82f));
        }
        if(race && rivalProgress>0)
        {
            int a=Math.Clamp((int)rivalProgress,0,Count-1), b=Math.Min(a+1,Count-1);
            var bp=Vector3.Lerp(platforms[a].Pos,platforms[b].Pos,rivalProgress-a)+new Vector3(0,1.8f+MathF.Abs(MathF.Sin(raceTime*9))*.4f,0);
            GL.PushMatrix();GL.Translate(bp.X,bp.Y-1,bp.Z);
            Box(new Vector3(0,0,0),new Vector3(.6f,1.25f,.45f),new Vector3(.9f,.65f,.12f));
            Box(new Vector3(0,.85f,0),new Vector3(.4f,.4f,.4f),new Vector3(1,.8f,.2f));
            GL.PopMatrix();
        }
        if(replay.Count>0 && ghostTime<=replay[^1].Time)
        {
            int idx=replay.FindLastIndex(f=>f.Time<=ghostTime);
            idx=Math.Max(0,idx);
            var g=replay[idx];
            GL.Enable(EnableCap.Blend);GL.BlendFunc(BlendingFactor.SrcAlpha,BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false);
            GL.PushMatrix();GL.Translate(g.Pos.X,g.Pos.Y-1.0f,g.Pos.Z);GL.Rotate(-g.Yaw,0,1,0);
            GhostBox(new Vector3(0,0,0),new Vector3(.55f,1.05f,.3f));
            GhostBox(new Vector3(0,.75f,0),new Vector3(.36f,.36f,.36f));
            GhostBox(new Vector3(-.38f,.05f,0),new Vector3(.18f,.8f,.2f));
            GhostBox(new Vector3(.38f,.05f,0),new Vector3(.18f,.8f,.2f));
            GhostBox(new Vector3(-.15f,-.75f,0),new Vector3(.2f,.65f,.22f));
            GhostBox(new Vector3(.15f,-.75f,0),new Vector3(.2f,.65f,.22f));
            GL.PopMatrix();GL.DepthMask(true);GL.Disable(EnableCap.Blend);
        }
        // First-person knife: fixed relative to camera, visible during inspection.
        GL.Clear(ClearBufferMask.DepthBufferBit);
        GL.LoadIdentity();
        GL.Translate(.55f,-.48f,-.85f);
        GL.Rotate(-25+MathF.Sin(inspect)* (inspectTime>0?130:0),0,1,0);
        GL.Rotate(15+MathF.Sin(inspect*1.3f)*(inspectTime>0?50:0),1,0,0);
        Box(new Vector3(0,-.13f,0),new Vector3(.09f,.3f,.09f),new Vector3(.15f,.13f,.12f));
        Box(new Vector3(0,.16f,0),new Vector3(.14f,.06f,.14f),new Vector3(.13f,.13f,.16f));
        Box(new Vector3(0,.46f,0),new Vector3(.065f,.55f,.025f),colors[knife]);
        DrawHud();
        SwapBuffers();
    }

    static void GhostBox(Vector3 center,Vector3 size)
    {
        GL.Color4(1f,.12f,.16f,.34f);
        float x=center.X,y=center.Y,z=center.Z,a=size.X/2,b=size.Y/2,d=size.Z/2;
        GL.Begin(PrimitiveType.Quads);
        foreach(var face in new [] {
            new[]{new Vector3(x-a,y-b,z-d),new Vector3(x+a,y-b,z-d),new Vector3(x+a,y+b,z-d),new Vector3(x-a,y+b,z-d)},
            new[]{new Vector3(x-a,y-b,z+d),new Vector3(x+a,y-b,z+d),new Vector3(x+a,y+b,z+d),new Vector3(x-a,y+b,z+d)},
            new[]{new Vector3(x-a,y-b,z-d),new Vector3(x-a,y-b,z+d),new Vector3(x-a,y+b,z+d),new Vector3(x-a,y+b,z-d)},
            new[]{new Vector3(x+a,y-b,z-d),new Vector3(x+a,y-b,z+d),new Vector3(x+a,y+b,z+d),new Vector3(x+a,y+b,z-d)},
            new[]{new Vector3(x-a,y+b,z-d),new Vector3(x+a,y+b,z-d),new Vector3(x+a,y+b,z+d),new Vector3(x-a,y+b,z+d)},
            new[]{new Vector3(x-a,y-b,z-d),new Vector3(x+a,y-b,z-d),new Vector3(x+a,y-b,z+d),new Vector3(x-a,y-b,z+d)}
        }) foreach(var v in face)GL.Vertex3(v);
        GL.End();
    }
    static readonly Dictionary<char,string> Glyphs = new()
    {
        ['A']="011101000110001111111000110001",['B']="11110100011000111110100011000111110",
        ['C']="01111100001000010000100001000001111",['D']="11110100011000110001100011000111110",
        ['E']="11111100001000011110100001000011111",['F']="11111100001000011110100001000010000",
        ['G']="01111100001000010111100011000101111",['H']="10001100011000111111100011000110001",
        ['I']="11111001000010000100001000010011111",['J']="00111000100001000010100101001001100",
        ['K']="10001100101010011000101001001010001",['L']="10000100001000010000100001000011111",
        ['M']="10001110111010110101100011000110001",['N']="10001110011010110011100011000110001",
        ['O']="01110100011000110001100011000101110",['P']="11110100011000111110100001000010000",
        ['Q']="01110100011000110001101011001001101",['R']="11110100011000111110101001001010001",
        ['S']="01111100001000001110000010000111110",['T']="11111001000010000100001000010000100",
        ['U']="10001100011000110001100011000101110",['V']="10001100011000110001100010101000100",
        ['W']="10001100011000110101101011101110001",['X']="10001100010101000100010101000110001",
        ['Y']="10001100010101000100001000010000100",['Z']="11111000010001000100010001000011111",
        ['0']="01110100011001110101110011000101110",['1']="00100011000010000100001000010001110",
        ['2']="01110100010000100010001000100011111",['3']="11110000010000101110000010000111110",
        ['4']="00010001100101010010111110001000010",['5']="11111100001111000001000011000101110",
        ['6']="01110100001000011110100011000101110",['7']="11111000010001000100010000100001000",
        ['8']="01110100011000101110100011000101110",['9']="01110100011000101111000010000101110",
        [':']="00000001000010000000001000010000000",['-']="00000000000000011111000000000000000",
        ['/']="00001000010001000100010001000000000",['<']="00001000100010001000001000001000001",
        ['>']="10000010000010000010001000100010000",['!']="00100001000010000100001000000000100"
    };
    static void Text2D(string text,float x,float y,float scale,Vector3 color)
    {
        GL.Color3(color);
        GL.Begin(PrimitiveType.Quads);
        foreach(char ch in text.ToUpperInvariant())
        {
            if(Glyphs.TryGetValue(ch,out var bits)) for(int j=0;j<Math.Min(35,bits.Length);j++) if(bits[j]=='1')
            {
                float px=x+(j%5)*scale,py=y+(j/5)*scale;
                GL.Vertex2(px,py);GL.Vertex2(px+scale*.85f,py);GL.Vertex2(px+scale*.85f,py+scale*.85f);GL.Vertex2(px,py+scale*.85f);
            }
            x+=6*scale;
        }
        GL.End();
    }
    static void Rect(float x,float y,float w,float h,Vector3 color)
    {
        GL.Color3(color);GL.Begin(PrimitiveType.Quads);
        GL.Vertex2(x,y);GL.Vertex2(x+w,y);GL.Vertex2(x+w,y+h);GL.Vertex2(x,y+h);GL.End();
    }
    void DrawHud()
    {
        GL.Disable(EnableCap.DepthTest);
        GL.MatrixMode(MatrixMode.Projection);GL.PushMatrix();GL.LoadIdentity();GL.Ortho(0,Size.X,Size.Y,0,-1,1);
        GL.MatrixMode(MatrixMode.Modelview);GL.PushMatrix();GL.LoadIdentity();
        Text2D($"LEVEL {level}   COINS {coins}   AUTO {(auto?"ON":"OFF")}",20,20,3,new Vector3(.9f,.95f,1));
        Text2D("TAB SHOP   B BOT   F INSPECT   R RACE   T CHAT",20,52,2,new Vector3(.6f,.8f,.95f));
        if(race) Text2D($"RACE {raceTime:0.0}  BOT {rivalProgress:0.0}/{Count-1}",20,82,3,new Vector3(1,.8f,.25f));
        lock(messages)
        {
            for(int i=0;i<messages.Count;i++) Text2D(messages[i],20,Size.Y-155+i*20,2,new Vector3(.8f,1,.85f));
        }
        if(chatting)
        {
            Rect(12,Size.Y-48,Size.X-24,38,new Vector3(.08f,.12f,.18f));
            Text2D("CHAT > "+chatInput+"_",20,Size.Y-38,2,new Vector3(1,1,1));
        }
        if(shop)
        {
            float cx=Size.X/2f,cy=Size.Y/2f;
            Rect(cx-310,cy-190,620,380,new Vector3(.06f,.09f,.15f));
            Rect(cx-305,cy-185,610,45,new Vector3(.14f,.23f,.34f));
            Text2D("KNIFE SHOP",cx-140,cy-174,5,new Vector3(1,.92f,.65f));
            Text2D($"COINS {coins}",cx-260,cy-107,3,new Vector3(.95f,.85f,.3f));
            Text2D($"< {names[selected]} >",cx-235,cy-43,5,new Vector3(.7f,.9f,1));
            Text2D((owned&(1<<selected))!=0?"OWNED":$"PRICE {prices[selected]}",cx-210,cy+35,4,new Vector3(.8f,1,.7f));
            Text2D((owned&(1<<selected))!=0?"ENTER EQUIP":"ENTER BUY",cx-210,cy+90,3,new Vector3(1,1,1));
            Text2D("LEFT RIGHT SELECT    TAB CLOSE",cx-265,cy+151,2,new Vector3(.65f,.75f,.85f));
        }
        GL.PopMatrix();GL.MatrixMode(MatrixMode.Projection);GL.PopMatrix();GL.MatrixMode(MatrixMode.Modelview);
        GL.Enable(EnableCap.DepthTest);
    }

    static void Box(Vector3 center,Vector3 size,Vector3 c)
    {
        float x=center.X,y=center.Y,z=center.Z,a=size.X/2,b=size.Y/2,d=size.Z/2;
        GL.Begin(PrimitiveType.Quads);
        void Face(float shade,params Vector3[] pts){GL.Color3(c.X*shade,c.Y*shade,c.Z*shade);foreach(var p in pts)GL.Vertex3(p);}
        Face(1,new(x-a,y+b,z-d),new(x+a,y+b,z-d),new(x+a,y+b,z+d),new(x-a,y+b,z+d));
        Face(.55f,new(x-a,y-b,z+d),new(x+a,y-b,z+d),new(x+a,y-b,z-d),new(x-a,y-b,z-d));
        Face(.8f,new(x-a,y-b,z+d),new(x-a,y+b,z+d),new(x+a,y+b,z+d),new(x+a,y-b,z+d));
        Face(.7f,new(x+a,y-b,z-d),new(x+a,y+b,z-d),new(x-a,y+b,z-d),new(x-a,y-b,z-d));
        Face(.65f,new(x-a,y-b,z-d),new(x-a,y+b,z-d),new(x-a,y+b,z+d),new(x-a,y-b,z+d));
        Face(.9f,new(x+a,y-b,z+d),new(x+a,y+b,z+d),new(x+a,y+b,z-d),new(x+a,y-b,z-d));
        GL.End();
    }
    protected override void OnUnload(){chatClient?.Dispose();Save();base.OnUnload();}
}
class Program { static void Main(string[] args){if(args.Contains("--server")){ChatServer.Run().GetAwaiter().GetResult();return;}using var game=new Game();game.Run();} }

// Simple LAN TCP chat. Start one server with: dotnet run -- --server
class ChatClient : IDisposable
{
    TcpClient? client;
    StreamWriter? writer;
    readonly Action<string> onMessage;
    public ChatClient(Action<string> onMessage) { this.onMessage=onMessage; }
    public void Connect(string host,int port)
    {
        _=Task.Run(async () =>
        {
            try
            {
                client=new TcpClient();await client.ConnectAsync(host,port);
                using var reader=new StreamReader(client.GetStream(),Encoding.UTF8);
                writer=new StreamWriter(client.GetStream(),Encoding.UTF8){AutoFlush=true};
                onMessage("CHAT CONNECTED");
                while(await reader.ReadLineAsync() is { } line)onMessage(line);
            }
            catch {onMessage("CHAT OFFLINE - START SERVER");}
        });
    }
    public void Send(string msg) {try {writer?.WriteLine(msg.Replace('\n',' '));}catch{}}
    public void Dispose(){client?.Close();}
}
class ChatServer
{
    public static async Task Run()
    {
        var listener=new TcpListener(IPAddress.Any,27777);listener.Start();
        Console.WriteLine("Tevrix chat server on TCP 27777. Ctrl+C to stop.");
        var clients=new List<StreamWriter>();var gate=new object();
        while(true)
        {
            var client=await listener.AcceptTcpClientAsync();
            _=Task.Run(async ()=>
            {
                StreamWriter? writer=null;
                try
                {
                    using(client)
                    using(var reader=new StreamReader(client.GetStream(),Encoding.UTF8))
                    {
                        writer=new StreamWriter(client.GetStream(),Encoding.UTF8){AutoFlush=true};
                        lock(gate)clients.Add(writer);
                        while(await reader.ReadLineAsync() is { } line)
                        {
                            if(line.Length>160)line=line[..160];
                            List<StreamWriter> targets;lock(gate)targets=clients.ToList();
                            foreach(var w in targets)try{await w.WriteLineAsync(line);}catch{}
                        }
                    }
                }
                catch(IOException){}catch(ObjectDisposedException){}
                finally{if(writer!=null)lock(gate)clients.Remove(writer);}
            });
        }
    }
}
