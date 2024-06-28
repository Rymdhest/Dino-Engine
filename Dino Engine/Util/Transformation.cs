

using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;
using System.Runtime.CompilerServices;

namespace Dino_Engine.Util
{
    public struct Transformation
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale { get; set; }

        public Transformation()
        {
            this.position = new Vector3(0f);
            this.rotation = new Vector3(0f);
            this.scale = new Vector3(1f);
        }
        public Transformation(Vector3 position, Vector3 rotation, Vector3 scale) {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
        public void translate(Vector3 translation)
        {
            position += translation;
        }
        public void translate(Vector4 translation)
        {
            translate(new Vector3(translation.X, translation.Y, translation.Z));
        }
        public void addRotation(Vector3 rotationAdd)
        {
            rotation += rotationAdd;
        }
        public void move(Vector3 direction)
        {
            Vector4 moveVector = new Vector4(direction.X, direction.Y, direction.Z, 1.0f);
            Matrix4 rotationMatrix = MyMath.createRotationMatrix(rotation);
            moveVector = rotationMatrix * moveVector;
            translate(moveVector);
        }
        public Vector3 createForwardVector()
        {
            return createForwardVector(new Vector3(0f, 0f, -1f));
        }
            public Vector3 createForwardVector(Vector3 forward)
        {
            Vector4 moveVector = new Vector4(forward.X, forward.Y, forward.Z, 1.0f);
            moveVector = MyMath.createRotationMatrix(rotation) * moveVector;
            moveVector.Normalize();
            return moveVector.Xyz;
        }

        public static Transformation Multiply(Transformation a, Transformation b)
        {
            Vector3 position = (new Vector4(a.position, 1.0f)*MyMath.createTransformationMatrix(b)).Xyz;
            Vector3 rotation = a.rotation + b.rotation;
            Vector3 scale = a.scale * b.scale;
            return new Transformation(position, rotation, scale);
        }

        public static Vector3 ApplyTransformationToVector3(Vector3 a, Transformation b)
        {
            return (new Vector4(a, 1.0f) * MyMath.createTransformationMatrix(b)).Xyz;
        }

        public static Transformation operator *(Transformation a, Transformation b) => Transformation.Multiply(a, b);

        public static Vector3 operator *(Vector3 a, Transformation b) => Transformation.ApplyTransformationToVector3(a, b);

        public override string ToString()
        {
            return $"Position: {position}\nRotation: {rotation}\nScale: {scale}";
        }

    }
}
