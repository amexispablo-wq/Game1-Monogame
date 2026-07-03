#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class PresentationManager : IDisposable
{
    private RenderTarget2D? _renderTarget;
    private int _logicalWidth = 1280;
    private int _logicalHeight = 720;
    private Rectangle _destinationRect;
    private bool _useLetterbox;

    public int LogicalWidth => _logicalWidth;
    public int LogicalHeight => _logicalHeight;
    public Rectangle DestinationRect => _destinationRect;
    public bool UsesLetterbox => _useLetterbox;

    public Viewport LogicalViewport => new(0, 0, _logicalWidth, _logicalHeight);

    public void Configure(GraphicsDevice graphicsDevice, int logicalWidth, int logicalHeight, bool letterbox)
    {
        _logicalWidth = Math.Max(1, logicalWidth);
        _logicalHeight = Math.Max(1, logicalHeight);
        _useLetterbox = letterbox;
        EnsureRenderTarget(graphicsDevice);
        UpdateDestination(graphicsDevice);
    }

    public void UpdateDestination(GraphicsDevice graphicsDevice)
    {
        int backWidth = graphicsDevice.PresentationParameters.BackBufferWidth;
        int backHeight = graphicsDevice.PresentationParameters.BackBufferHeight;

        if (!_useLetterbox || backWidth == _logicalWidth && backHeight == _logicalHeight)
        {
            _destinationRect = new Rectangle(0, 0, backWidth, backHeight);
            return;
        }

        float scale = MathF.Min(backWidth / (float)_logicalWidth, backHeight / (float)_logicalHeight);
        int destWidth = Math.Max(1, (int)MathF.Floor(_logicalWidth * scale));
        int destHeight = Math.Max(1, (int)MathF.Floor(_logicalHeight * scale));
        int destX = (backWidth - destWidth) / 2;
        int destY = (backHeight - destHeight) / 2;
        _destinationRect = new Rectangle(destX, destY, destWidth, destHeight);
    }

    public void BeginDraw(GraphicsDevice graphicsDevice)
    {
        EnsureRenderTarget(graphicsDevice);
        UpdateDestination(graphicsDevice);
        graphicsDevice.SetRenderTarget(_renderTarget);
    }

    public void EndDraw(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Texture2D pixel, Color letterboxColor)
    {
        if (_renderTarget is null)
        {
            return;
        }

        graphicsDevice.SetRenderTarget(null);
        graphicsDevice.Clear(letterboxColor);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_renderTarget, _destinationRect, Color.White);
        spriteBatch.End();
    }

    public Point MapPointerToLogical(Point backBufferPointer)
    {
        if (!_useLetterbox
            || (_destinationRect.Width == _logicalWidth && _destinationRect.Height == _logicalHeight))
        {
            return backBufferPointer;
        }

        if (_destinationRect.Width <= 0 || _destinationRect.Height <= 0)
        {
            return backBufferPointer;
        }

        float localX = backBufferPointer.X - _destinationRect.X;
        float localY = backBufferPointer.Y - _destinationRect.Y;
        if (localX < 0f || localY < 0f
            || localX > _destinationRect.Width || localY > _destinationRect.Height)
        {
            return new Point(-1, -1);
        }

        int logicalX = (int)MathF.Round(localX * _logicalWidth / _destinationRect.Width);
        int logicalY = (int)MathF.Round(localY * _logicalHeight / _destinationRect.Height);
        return new Point(logicalX, logicalY);
    }

    private void EnsureRenderTarget(GraphicsDevice graphicsDevice)
    {
        if (_renderTarget is not null
            && _renderTarget.Width == _logicalWidth
            && _renderTarget.Height == _logicalHeight)
        {
            return;
        }

        _renderTarget?.Dispose();
        _renderTarget = new RenderTarget2D(
            graphicsDevice,
            _logicalWidth,
            _logicalHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None);
    }

    public void Dispose()
    {
        _renderTarget?.Dispose();
        _renderTarget = null;
    }
}
