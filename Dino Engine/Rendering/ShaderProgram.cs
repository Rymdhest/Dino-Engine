
using Dino_Engine.Core;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Xml.Linq;

namespace Dino_Engine.Rendering
{
    public class ShaderProgram
    {
        private int programID;
        private int vertexShaderID;
        private int fragmentShaderID;
        private int geometryShaderID;
        private static string _shaderFolderPath = "../../../../Dino Engine/Shaders/";
        private Dictionary<string, int> uniforms;
        public ShaderProgram(string vertexFile, string fragmentFile)
        {
            vertexShaderID = loadShader(vertexFile, ShaderType.VertexShader);
            fragmentShaderID = loadShader(fragmentFile, ShaderType.FragmentShader);
            programID = GL.CreateProgram();

            Console.WriteLine($"loading program {programID} from {vertexFile} and {fragmentFile}");

            GL.AttachShader(programID, vertexShaderID);
            GL.AttachShader(programID, fragmentShaderID);
            GL.LinkProgram(programID);
            GL.ValidateProgram(programID);

            uniforms = new Dictionary<string, int>();
            extractAllUniformsToDictionary(vertexFile);
            extractAllUniformsToDictionary(fragmentFile);



        }
        public ShaderProgram(string vertexFile, string fragmentFile, string geometryFile)
        {
            vertexShaderID = loadShader(vertexFile, ShaderType.VertexShader);
            fragmentShaderID = loadShader(fragmentFile, ShaderType.FragmentShader);
            geometryShaderID = loadShader(geometryFile, ShaderType.GeometryShader);
            programID = GL.CreateProgram();
            GL.AttachShader(programID, vertexShaderID);
            GL.AttachShader(programID, fragmentShaderID);
            GL.AttachShader(programID, geometryShaderID);
            GL.LinkProgram(programID);
            GL.ValidateProgram(programID);

            uniforms = new Dictionary<string, int>();
            extractAllUniformsToDictionary(vertexFile);
            extractAllUniformsToDictionary(fragmentFile);
            extractAllUniformsToDictionary(geometryFile);

        }
        public void loadUniformInt(string variableName, int value)
        {
            GL.Uniform1(uniforms[variableName], value);
        }
        public void loadUniformIntArray(string variableName, int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                GL.Uniform1(uniforms[variableName + "[" + i + "]"], values[i]);
            }
        }
        public void loadUniformBool(string variableName, bool value)
        {
            float floatValue = 0f;
            if (value) floatValue = 1.0f;
            GL.Uniform1(uniforms[variableName], floatValue);
        }

        public void loadUniformFloat(string variableName, float value)
        {
            GL.Uniform1(uniforms[variableName], value);
        }
        public void loadUniformFloatArray(string variableName, float[] values)
        {
            for (int i = 0; i<values.Length; i++)
            {
                GL.Uniform1(uniforms[variableName+"["+i+"]"], values[i]);
            }
        }


        public void loadUniformVector2f(string variableName, Vector2 value)
        {
            GL.Uniform2(uniforms[variableName], value);
        }
        public void loadUniformVector2fArray(string variableName, Vector2[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                GL.Uniform2(uniforms[variableName + "[" + i + "]"], values[i]);
            }
        }


        public void loadUniformVector3f(string variableName, Vector3 value)
        {
            GL.Uniform3(uniforms[variableName], value);
        }
        public void loadUniformVector3fArray(string variableName, Vector3[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                GL.Uniform3(uniforms[variableName + "[" + i + "]"], values[i]);
            }
        }


        public void loadUniformVector4f(string variableName, Vector4 value)
        {
            GL.Uniform4(uniforms[variableName], value);
        }
        public void loadUniformVector4fArray(string variableName, Vector4[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                GL.Uniform4(uniforms[variableName + "[" + i + "]"], values[i]);
            }
        }


        public void loadUniformMatrix4f(string variableName, Matrix4 value)
        {
            GL.UniformMatrix4(uniforms[variableName],true, ref value);
        }
        public void loadUniformMatrix4fArray(string variableName, Matrix4[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                GL.UniformMatrix4(uniforms[variableName + "[" + i + "]"],true, ref values[i]);
            }
        }

        private int loadShader(string name, ShaderType type)
        {
            string fullPath = _shaderFolderPath+"/" + name;
            int shaderID = GL.CreateShader(type);


            string fileString = "";
            try
            {
                fileString = File.ReadAllText(fullPath);
            } catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }

            GL.ShaderSource(shaderID, fileString);
            GL.CompileShader(shaderID);

            GL.GetShader(shaderID, ShaderParameter.CompileStatus, out var code);
            if (code != (int)All.True){
                string infoLog = GL.GetShaderInfoLog(shaderID);
                Console.WriteLine($"Could not compile shader ({name}).\n\n{infoLog}");
            }


            return shaderID;
        }

        private void extractAllUniformsToDictionary(string fileName)
        {
            string fullPath = _shaderFolderPath + "/" + fileName;
            try
            {
                foreach (string line in File.ReadLines(fullPath))
                {
                    string[] words = line.Split(" ");
                    if (words.Length == 3)
                    {
                        if (words[0] == "uniform")
                        {
                            string variableName = words[2].Remove(words[2].Length - 1, 1);
                            if (variableName.Last<char>() == ']')
                            {
                                string[] variableWords = variableName.Split("[");
                                string arraySizeString = variableWords[1];
                                arraySizeString = arraySizeString.Remove(arraySizeString.Length - 1, 1);
                                int arraySize = int.Parse(arraySizeString);
                                variableName = variableWords[0];
                                for (int i = 0; i < arraySize; i++)
                                {
                                    loadUniform(variableName + "[" + i + "]");
                                }
                            }
                            else
                            {
                                loadUniform(variableName);
                            }


                        }
                    }
                }
            }
            catch (Exception e)
            {
                return;
            }

        }
        private void loadUniform(string variableName)
        {
            if (!uniforms.ContainsKey(variableName))
            {
                int location = GL.GetUniformLocation(programID, variableName);
                if (location == -1)
                {
                    //Console.WriteLine("Something went wrong getting uniform for " + variableName + " in " + fileName + " maybe the variable is not used in shader?");
                }
                
                uniforms.Add(variableName, location);
            }
        }
        public void bind()
        {
            GL.UseProgram(programID);
        }
        public void unBind()
        {
            GL.UseProgram(0);
        }
        public void cleanUp()
        {
            GL.DetachShader(programID, vertexShaderID);
            GL.DetachShader(programID, fragmentShaderID);
            GL.DetachShader(programID, geometryShaderID);
            GL.DeleteShader(vertexShaderID);
            GL.DeleteShader(fragmentShaderID);
            GL.DeleteShader(geometryShaderID);
            GL.DeleteProgram(programID);
        }
        public void printAllUniforms()
        {
            foreach (KeyValuePair<string, int> kvp in uniforms)
            {
                //textBox3.Text += ("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
                Console.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
            }
        }
    }
}
