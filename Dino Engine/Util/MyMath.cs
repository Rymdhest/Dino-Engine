using OpenTK.Mathematics;

namespace Dino_Engine.Util
{
    internal class MyMath
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
        public static float lerp(float amount, float left, float right)
        {
            return (1.0f - amount) * left + amount * right;
        }
        public static Vector3 lerp(float amount, Vector3 left, Vector3 right)
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
    }
}
