
using OpenTK.Mathematics;
using Dino_Engine.Util;
namespace Dino_Engine.Modelling
{
    internal class RawModel
    {
        public float[] positions;
        public float[] colors;
        public float[] normals;
        public float[] materials;
        public int[] indices;

        public RawModel()
        : this(new float[0], new float[0], new float[0], new float[0], new int[0])
        {
        }
        public RawModel(List<Vector3> positions, List<Vector3> colours, List<int> indices)
            : this(fromVector3ListToArray(positions), fromVector3ListToArray(colours), fromIntListToArray(indices))
        {
        }

        public RawModel(float[] positions, float[] colors, int[] indices)
            : this(positions, colors,new float[0], new float[0], indices)
        {
            materials = new float[positions.Length];
            for (int i = 0; i < positions.Length; i+=3)
            {
                materials[i+0] = 0.5f;
                materials[i+1] = 0.0f;
                materials[i+2] = 0.0f;
            }
        }
        public RawModel(float[] positions, float[] colors, float[] normals, int[] indices)
    : this(positions, colors, new float[0], normals, indices)
        {
            materials = new float[positions.Length];
            for (int i = 0; i < positions.Length; i+=3)
            {
                materials[i+0] = 0.5f;
                materials[i+1] = 0.0f;
                materials[i+2] = 0.0f;
            }
        }


        public RawModel(float[] positions, float[] colors, float[] materials, float[] normals, int[] indices)
        {
            this.positions = positions;
            this.colors = colors;
            this.normals = normals;
            this.materials = materials;
            this.indices = indices;


        }

        public void setRoughness(float setTo)
        {
            for (int i = 0; i < materials.Length; i += 3)
            {
                materials[i] = setTo;
            }
        }
        public void setEmission(float setTo)
        {
            for (int i = 0; i < materials.Length; i += 3)
            {
                materials[i+1] = setTo;
            }
        }
        public void setMetalicness(float setTo)
        {
            for (int i = 0; i < materials.Length; i += 3)
            {
                materials[i + 2] = setTo;
            }
        }
        public void flatRandomness(float amount)
        {
            flatRandomness(new Vector3(amount));
        }
        public void flatRandomness(Vector3 amount)
        {
            Random rand = new Random();
            for (int i = 0; i < positions.Length; i += 3)
            {
                positions[i] += MyMath.rngMinusPlus() * amount.X;
                positions[i + 1] += MyMath.rngMinusPlus() * amount.Y;
                positions[i + 2] += MyMath.rngMinusPlus() * amount.Z;
            }
        }


        public void scaleRandomness(float amount)
        {
            scaleRandomness(new Vector3(amount));
        }
        public void scaleRandomness(Vector3 amount)
        {
            Random rand = new Random();
            for (int i = 0; i < positions.Length; i += 3)
            {
                positions[i] *= 1f+ MyMath.rngMinusPlus() * amount.X;
                positions[i + 1] *= 1f+ MyMath.rngMinusPlus() * amount.Y;
                positions[i + 2] *= 1f+ MyMath.rngMinusPlus() * amount.Z;
            }
        }
        private static float[] fromVector3ListToArray(List<Vector3> list)
        {
            float[] array = new float[list.Count * 3];

            for (int i = 0; i < list.Count; i++)
            {
                array[i * 3] = list[i].X;
                array[i * 3 + 1] = list[i].Y;
                array[i * 3 + 2] = list[i].Z;
            }
            return array;
        }
        private static int[] fromIntListToArray(List<int> list)
        {
            int[] array = new int[list.Count];

            for (int i = 0; i < list.Count; i++)
            {
                array[i] = list[i];
            }
            return array;
        }
        public void cleanUp()
        {

        }

        public void translate(Vector3 translation)
        {
            for (int i = 0; i < positions.Length; i += 3)
            {
                positions[i] += translation.X;
                positions[i + 1] += translation.Y;
                positions[i + 2] += translation.Z;
            }
        }
        public void rotate(Vector3 rot)
        {
            var rotMatrix = MyMath.createRotationMatrix(rot);
            for (int i = 0; i < positions.Length; i += 3)
            {
                Vector4 pos = new Vector4(positions[i], positions[i+1], positions[i+2], 1.0f);
                pos = rotMatrix*pos;
                positions[i] = pos.X;
                positions[i+1] = pos.Y;
                positions[i+2] = pos.Z;
            }
        }
        public void merge(RawModel other)
        {
            float[] newPositions = new float[positions.Length+other.positions.Length];
            float[] newColours = new float[colors.Length + other.colors.Length];
            float[] newMaterials = new float[materials.Length + other.materials.Length];
            float[] newnNrmals = new float[normals.Length + other.normals.Length];
            int[] newIndices = new int[indices.Length + other.indices.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                newPositions[i] = positions[i];
                newColours[i] = colors[i];
                newMaterials[i] = materials[i];
            }
            for (int i = 0; i < normals.Length; i++)
            {
                newnNrmals[i] = normals[i];
            }
            for (int i = 0; i < indices.Length; i++)
            {
                newIndices[i] = indices[i];
            }
            for (int i = 0; i < other.positions.Length; i++)
            {
                newPositions[positions.Length + i] = other.positions[i];
                newColours[positions.Length + i] = other.colors[i];
                newMaterials[positions.Length + i] = other.materials[i];
            }
            for (int i = 0; i < other.normals.Length; i++)
            {
                newnNrmals[normals.Length + i] = other.normals[i];
            }
            for (int i = 0; i < other.indices.Length; i++)
            {
                newIndices[indices.Length + i] = other.indices[i] + positions.Length / 3;
            }
            positions = newPositions;
            colors = newColours;
            materials = newMaterials;
            normals = newnNrmals;
            indices = newIndices;
        }
    }
}
