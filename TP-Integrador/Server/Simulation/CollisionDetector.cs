using SpaceShooter.GameDomain;

namespace SpaceShooter.Server.Simulation;

/// <summary>
/// Detects intersections between entity hitboxes without applying gameplay rules.
/// </summary>
internal static class CollisionDetector
{
  public static bool Intersects(Entity first, Entity second)
  {
    Vector2D firstCenter = first.Hitbox.GetCenter(first.Position);
    Vector2D secondCenter = second.Hitbox.GetCenter(second.Position);
    return Intersects(first.Hitbox, firstCenter, second.Hitbox, secondCenter);
  }

  public static bool Intersects(
      Hitbox first,
      Vector2D firstCenter,
      Hitbox second,
      Vector2D secondCenter)
  {
    return (first.Shape, second.Shape) switch
    {
      (CircleShape firstCircle, CircleShape secondCircle) =>
          CirclesIntersect(firstCircle, firstCenter, secondCircle, secondCenter),
      (RectangleShape firstRectangle, RectangleShape secondRectangle) =>
          RectanglesIntersect(firstRectangle, firstCenter, secondRectangle, secondCenter),
      (CircleShape circle, RectangleShape rectangle) =>
          CircleRectangleIntersect(circle, firstCenter, rectangle, secondCenter),
      (RectangleShape rectangle, CircleShape circle) =>
          CircleRectangleIntersect(circle, secondCenter, rectangle, firstCenter),
      _ => false,
    };
  }

  private static bool CirclesIntersect(
      CircleShape first,
      Vector2D firstPosition,
      CircleShape second,
      Vector2D secondPosition)
  {
    float deltaX = firstPosition.X - secondPosition.X;
    float deltaY = firstPosition.Y - secondPosition.Y;
    float radius = first.Radius + second.Radius;
    return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
  }

  private static bool RectanglesIntersect(
      RectangleShape first,
      Vector2D firstPosition,
      RectangleShape second,
      Vector2D secondPosition)
  {
    return MathF.Abs(firstPosition.X - secondPosition.X) <= (first.Width + second.Width) / 2f &&
           MathF.Abs(firstPosition.Y - secondPosition.Y) <= (first.Height + second.Height) / 2f;
  }

  private static bool CircleRectangleIntersect(
      CircleShape circle,
      Vector2D circlePosition,
      RectangleShape rectangle,
      Vector2D rectanglePosition)
  {
    float halfWidth = rectangle.Width / 2f;
    float halfHeight = rectangle.Height / 2f;
    float nearestX = Math.Clamp(
        circlePosition.X,
        rectanglePosition.X - halfWidth,
        rectanglePosition.X + halfWidth);
    float nearestY = Math.Clamp(
        circlePosition.Y,
        rectanglePosition.Y - halfHeight,
        rectanglePosition.Y + halfHeight);
    float deltaX = circlePosition.X - nearestX;
    float deltaY = circlePosition.Y - nearestY;
    return (deltaX * deltaX) + (deltaY * deltaY) <= circle.Radius * circle.Radius;
  }
}
