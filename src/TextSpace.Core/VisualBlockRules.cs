namespace TextSpace.Core;

public static class VisualBlockRules
{
    public static void Validate(VisualBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (!double.IsFinite(block.Width + block.Height) || block.Width is <= 0 or > 4000 || block.Height is <= 0 or > 4000
            || !Enum.IsDefined(block.Alignment) || block.Placement is not { } p
            || !double.IsFinite(p.X + p.Y + p.Rotation) || Math.Abs(p.X) > 4000 || Math.Abs(p.Y) > 4000 || Math.Abs(p.Rotation) > 36000)
            throw new InvalidDataException("Invalid visual object geometry.");
        switch (block)
        {
            case ImageBlock image:
                if (image.Data is null || image.Crop is not { } c || !double.IsFinite(c.Left + c.Top + c.Right + c.Bottom)
                    || c.Left < 0 || c.Top < 0 || c.Right < 0 || c.Bottom < 0 || c.Left + c.Right >= 0.99 || c.Top + c.Bottom >= 0.99)
                    throw new InvalidDataException("Invalid non-destructive picture crop.");
                break;
            case EquationBlock equation:
                if (!double.IsFinite(equation.FontSize) || equation.FontSize is < 6 or > 200 || !IsColor(equation.Color))
                    throw new InvalidDataException("Invalid equation formatting.");
                EquationRules.Validate(equation.Root); break;
            case ShapeBlock shape:
                if (!Enum.IsDefined(shape.Kind) || !Enum.IsDefined(shape.VerticalAlignment) || shape.Text is null || shape.Text.Length > 100000
                    || shape.TextStyle is null || !double.IsFinite(shape.TextStyle.FontSize) || shape.TextStyle.FontSize is < 1 or > 400
                    || !IsColor(shape.Stroke) || shape.Fill is not null && !IsColor(shape.Fill)
                    || !double.IsFinite(shape.StrokeWidth + shape.Padding + shape.CornerRadius) || shape.StrokeWidth is < 0 or > 72
                    || shape.Padding is < 0 or > 72 || shape.CornerRadius is < 0 or > 4000)
                    throw new InvalidDataException("Invalid shape formatting.");
                break;
        }
    }

    public static bool IsColor(string? value) => value is not null && (value.Length == 7 || value.Length == 9)
        && value[0] == '#' && value.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF".AsSpan()) == false;
}
