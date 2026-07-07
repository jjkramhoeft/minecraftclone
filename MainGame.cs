using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Rendering;

namespace MinecraftClone;

public class MainGame : Game
{
    private const float FlySpeed = 10f;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private FirstPersonCamera _camera;
    private DebugCube _debugCube;

    // False whenever the window lost focus, so the first recentering of the
    // mouse doesn't register as a huge look delta.
    private bool _mouseCaptured;

    public MainGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        Window.Title = "Minecraft Clone";
    }

    protected override void Initialize()
    {
        _camera = new FirstPersonCamera
        {
            Position = new Vector3(0f, 1.5f, -5f),
            Yaw = 0f,          // looking toward +Z, at the cube
            Pitch = -0.28f,    // tilted slightly down toward the origin
        };
        _camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _debugCube = new DebugCube(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
            Exit();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        UpdateMouseLook();
        UpdateFlyMovement(keyboard, dt);

        base.Update(gameTime);
    }

    private void UpdateMouseLook()
    {
        if (!IsActive)
        {
            _mouseCaptured = false;
            return;
        }

        int centerX = Window.ClientBounds.Width / 2;
        int centerY = Window.ClientBounds.Height / 2;
        var mouse = Mouse.GetState();

        if (_mouseCaptured)
            _camera.Look(mouse.X - centerX, mouse.Y - centerY);

        Mouse.SetPosition(centerX, centerY);
        _mouseCaptured = true;
    }

    private void UpdateFlyMovement(KeyboardState keyboard, float dt)
    {
        var move = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) move += _camera.HorizontalForward;
        if (keyboard.IsKeyDown(Keys.S)) move -= _camera.HorizontalForward;
        if (keyboard.IsKeyDown(Keys.D)) move += _camera.Right;
        if (keyboard.IsKeyDown(Keys.A)) move -= _camera.Right;
        if (move != Vector3.Zero) move.Normalize();
        if (keyboard.IsKeyDown(Keys.Space)) move += Vector3.Up;
        if (keyboard.IsKeyDown(Keys.LeftShift)) move -= Vector3.Up;

        _camera.Position += move * FlySpeed * dt;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;

        _debugCube.Draw(GraphicsDevice, _camera.View, _camera.Projection);

        base.Draw(gameTime);
    }
}
