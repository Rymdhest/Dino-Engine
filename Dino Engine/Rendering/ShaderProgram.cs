
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
        private Dictionary<string, int> uniforms = new Dictionary<string, int>();
        public ShaderProgram(string vertexFile, string fragmentFile)
        {
            string vertexString = PreProccessShaderFromFile(vertexFile);
            vertexShaderID = CreateShader(vertexString, ShaderType.VertexShader);

            string fragmentString = PreProccessShaderFromFile(fragmentFile);
            fragmentShaderID = CreateShader(fragmentString, ShaderType.FragmentShader);
            programID = GL.CreateProgram();

            GL.AttachShader(programID, vertexShaderID);
            GL.AttachShader(programID, fragmentShaderID);
            GL.LinkProgram(programID);
            GL.ValidateProgram(programID);

            LoadAllUniformsFromShaderString(vertexString);
            LoadAllUniformsFromShaderString(fragmentString);
        }
        public ShaderProgram(string vertexFile, string fragmentFile, string geometryFile)
        {
            string vertexString = PreProccessShaderFromFile(vertexFile);
            vertexShaderID = CreateShader(vertexString, ShaderType.VertexShader);

            string fragmentString = PreProccessShaderFromFile(fragmentFile);
            fragmentShaderID = CreateShader(fragmentString, ShaderType.FragmentShader);

            string geometryString = PreProccessShaderFromFile(geometryFile);
            geometryShaderID = CreateShader(geometryString, ShaderType.GeometryShader);
            programID = GL.CreateProgram();
            GL.AttachShader(programID, vertexShaderID);
            GL.AttachShader(programID, fragmentShaderID);
            GL.AttachShader(programID, geometryShaderID);
            GL.LinkProgram(programID);
            GL.ValidateProgram(programID);

            LoadAllUniformsFromShaderString(vertexString);
            LoadAllUniformsFromShaderString(fragmentString);
            LoadAllUniformsFromShaderString(geometryString);
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

        private int CreateShader(string shaderString, ShaderType type)
        {
            int shaderID = GL.CreateShader(type);

            GL.ShaderSource(shaderID, shaderString);
            GL.CompileShader(shaderID);

            GL.GetShader(shaderID, ShaderParameter.CompileStatus, out var code);
            if (code != (int)All.True){
                string infoLog = GL.GetShaderInfoLog(shaderID);
                Console.WriteLine($"Could not compile shader.\n\n{infoLog}");
            }


            return shaderID;
        }

        private void LoadAllUniformsFromShaderString(string shaderString)
        {
            using (var reader = new StringReader(shaderString))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("uniform"))
                    {
                        string[] words = line.Split(" ");
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

        private string PreProccessShaderFromFile(string fileName)
        {
            string fullPath = _shaderFolderPath + "/" + fileName;
            var processedShader = new System.Text.StringBuilder();

            foreach (string line in File.ReadLines(fullPath))
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("#include"))
                {

                    string[] words = trimmedLine.Split(" ");
                    processedShader.AppendLine(PreProccessShaderFromFile(words[1]));
                }
                else
                {
                    processedShader.AppendLine(line);
                }
            }
            return processedShader.ToString();

        }
        private void loadUniform(string variableName)
        {
            if (!uniforms.ContainsKey(variableName))
            {
                int location = GL.GetUniformLocation(programID, variableName);
                if (location == -1)
                {
                    Console.WriteLine("Something went wrong getting uniform for " + variableName + " in maybe the variable is not used in shader?");
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
