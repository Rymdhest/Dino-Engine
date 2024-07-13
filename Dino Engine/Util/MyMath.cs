using OpenTK.Mathematics;

namespace Dino_Engine.Util
{
    public class MyMath
    {
        public static Random rand = new Random();

        public static Vector3 rng3D(float scale = 1.0f)
        {
            return new Vector3(rng(), rng(), rng()) * scale;
        }
        public static Vector3 rng3DMinusPlus(float scale = 1.0f)
        {
            return (new Vector3(rngMinusPlus(scale), rngMinusPlus(scale), rngMinusPlus(scale)));
        }
        public static Vector2 rng2D(float scale = 1.0f)
        {
            return new Vector2(rng(), rng()) * scale;
        }
        public static Vector2 rng2DMinusPlus(float scale = 1.0f)
        {
            return (new Vector2(rngMinusPlus(scale), rngMinusPlus(scale)));
        }
        public static float rngMinusPlus(float scale = 1.0f)
        {
            return (rand.NextSingle() * 2f - 1f)* scale;
        }
        public static float rng(float scale = 1.0f)
        {
            return rand.NextSingle()*scale;
        }
  
        public static Vector3 reflect(Vector3 vector, Vector3 normal)
        {
            return vector-(2f * Vector3.Dot(normal, vector)*normal);
        }

        public static Matrix4 createTransformationMatrix(Transformation transformation)
        
        {
            return createTransformationMatrix(transformation.position, transformation.rotation, transformation.scale);
        }
        public static Matrix4 createTransformationMatrix(Vector3 position)
        {
            return createTransformationMatrix(position, new Vector3(0), new Vector3(0));
        }
        public static Matrix4 createTransformationMatrix(Vector3 position, float scale)
        {
            return createTransformationMatrix(position, new Vector3(0), new Vector3(scale));
        }
        public static Matrix4 createTransformationMatrix(Vector3 position, Vector3 rotation, float scale)
        {
            return createTransformationMatrix(position, rotation, new Vector3(scale));
        }
        public static Matrix4 createTransformationMatrix(Vector3 position, Vector3 rotation, Vector3 scale)
        {

            Matrix4 matrix = Matrix4.Identity;
            matrix = matrix * Matrix4.CreateScale(scale);
            matrix = matrix * createRotationMatrix(rotation);
            matrix = matrix * Matrix4.CreateTranslation(position);
            return matrix;
        }
        public static Matrix4 createViewMatrix(Transformation transformation)
        {
            return createViewMatrix(transformation.position, transformation.rotation);
        }
        public static Matrix4 createViewMatrix(Vector3 position, Vector3 rotation)
        {

            Matrix4 matrix = Matrix4.Identity;
            matrix = matrix * Matrix4.CreateTranslation(-position);
            matrix = matrix * createRotationMatrix(rotation);
            return matrix;
        }
        public static Matrix4 createRotationMatrix(Vector3 rotation)
        {

            Matrix4 matrix = Matrix4.Identity;
            matrix = matrix * Matrix4.CreateRotationZ(rotation.Z);
            matrix = matrix * Matrix4.CreateRotationY(rotation.Y);
            matrix = matrix * Matrix4.CreateRotationX(rotation.X);
            return matrix;
        }
        public static float clamp(float number, float min, float max)
        {
            if (number < min) return min;
            if (number > max) return max;
            return number;
        }
        public static float clamp01(float number)
        {
            return clamp(number, 0.0f, 1.0f);
        }
        public static float lerp(float left, float right, float amount)
        {
            return (1.0f - amount) * left + amount * right;
        }
        public static Vector3 lerp(Vector3 left, Vector3 right, float amount)
        {
            return (1.0f - amount) * left + amount * right;
        }
        public static Vector3 calculateFaceNormal(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            float aX, aY, aZ, bX, bY, bZ;

            aX = v2.X - v1.X;
            aY = v2.Y - v1.Y;
            aZ = v2.Z - v1.Z;

            bX = v3.X - v1.X;
            bY = v3.Y - v1.Y;
            bZ = v3.Z - v1.Z;

            Vector3 normal = new Vector3((aY * bZ) - (aZ * bY), (aZ * bX) - (aX * bZ), (aX * bY) - (aY * bX));
            normal.Normalize();
            return normal;
        }
        public static float barryCentric(Vector3 p1, Vector3 p2, Vector3 p3, Vector2 pos)
        {
            float det = (p2.Z - p3.Z) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Z - p3.Z);
            float l1 = ((p2.Z - p3.Z) * (pos.X - p3.X) + (p3.X - p2.X) * (pos.Y - p3.Z)) / det;
            float l2 = ((p3.Z - p1.Z) * (pos.X - p3.X) + (p1.X - p3.X) * (pos.Y - p3.Z)) / det;
            float l3 = 1.0f - l1 - l2;
            return l1 * p1.Y + l2 * p2.Y + l3 * p3.Y;
        }

        public static float AngleBetween(Vector2 p1, Vector2 p2)
        {
            return MathF.Atan2(p2.Y - p1.Y, p2.X - p1.X);
        }

        public static Vector3 BilinearInterpolateNormal(Vector3[,] grid, float x, float y)
        {
            int x1 = (int)Math.Floor(x);
            int x2 = x1 + 1;
            int y1 = (int)Math.Floor(y);
            int y2 = y1 + 1;

            float t_x = x - x1;
            float t_y = y - y1;

            // Ensure the indices are within the bounds of the grid
            x1 = Math.Clamp(x1, 0, grid.GetLength(0) - 1);
            x2 = Math.Clamp(x2, 0, grid.GetLength(0) - 1);
            y1 = Math.Clamp(y1, 0, grid.GetLength(1) - 1);
            y2 = Math.Clamp(y2, 0, grid.GetLength(1) - 1);

            // Get the normals from the Vector3 points
            Vector3 Q11 = grid[x1, y1];
            Vector3 Q12 = grid[x2, y1];
            Vector3 Q21 = grid[x1, y2];
            Vector3 Q22 = grid[x2, y2];

            // Interpolate along the x-axis (bottom and top rows)
            Vector3 R1 = Vector3.Lerp(Q11, Q12, t_x);
            Vector3 R2 = Vector3.Lerp(Q21, Q22, t_x);

            // Interpolate along the y-axis (left and right columns)
            Vector3 interpolatedNormal = Vector3.Lerp(R1, R2, t_y);

            // Normalize the resulting normal to ensure it is a unit vector
            interpolatedNormal = Vector3.Normalize(interpolatedNormal);

            return interpolatedNormal;
        }
    }
}
