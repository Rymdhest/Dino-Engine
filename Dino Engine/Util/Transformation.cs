

using OpenTK.Mathematics;

namespace Dino_Engine.Util
{
    public struct Transformation
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale { get; set; }
        public Transformation(float posX = 0.0f, float posY = 0.0f, float posZ = 0.0f, 
            float rotX = 0.0f, float rotY = 0.0f, float rotZ = 0.0f, float scale = 1.0f)
            : this(new Vector3(posX, posY, posZ), new Vector3(rotX, rotY, rotZ), new Vector3(scale)) { }
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

        public override string ToString()
        {
            return $"Position: {position}\nRotation: {rotation}\nScale: {scale}";
        }

    }
}
