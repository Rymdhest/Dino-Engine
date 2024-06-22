using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Modelling.Model;

internal class glModel
{
    private int vaoID;
    private int[] VBOS;
    private int vertexCount;

    public glModel(int vaoID, int[] VBOS, int vertexCount)
    {
        this.vaoID = vaoID;
        this.VBOS = VBOS;
        this.vertexCount = vertexCount;
    }

    public int getVAOID()
    {
        return vaoID;
    }


    public int getVertexCount()
    {
        return vertexCount;
    }

    public void cleanUp()
    {
        GL.DeleteVertexArray(vaoID);
        for (int i = 0; i < VBOS.Length; i++)
        {
            GL.DeleteBuffer(VBOS[i]);
        }
    }
}
