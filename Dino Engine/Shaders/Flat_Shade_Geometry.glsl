#version 330
layout(triangles) in;
layout(triangle_strip, max_vertices=3) out;

in vec3 geoColor[];
in vec3 positionViewSpace[];
in vec3 geoMaterials[];

out vec3 fragColor;
out vec3 fragNormal;
out vec3 fragMaterials;
out vec3 positionViewSpace_pass;

vec3 calculateFaceNormal(vec3 v1, vec3 v2, vec3 v3) {
		vec3 normal = vec3(0.0, 0.0, 0.0);

		float aX, aY, aZ, bX, bY, bZ;


		aX = v2.x - v1.x;
		aY = v2.y - v1.y;
		aZ = v2.z - v1.z;

		bX = v3.x - v1.x;
		bY = v3.y - v1.y;
		bZ = v3.z - v1.z;

		normal.x = (aY*bZ)-(aZ*bY);
		normal.y = (aZ*bX)-(aX*bZ);
		normal.z = (aX*bY)-(aY*bX);

		return normal;
	}

void main()
{	
	//vec3 v1 = gl_in[0].gl_Position.xyz;
	//vec3 v2 = gl_in[1].gl_Position.xyz;
	//vec3 v3 = gl_in[2].gl_Position.xyz;

	vec3 v1 = positionViewSpace[0];
	vec3 v2 = positionViewSpace[1];
	vec3 v3 = positionViewSpace[2];

	vec3 faceNormal = calculateFaceNormal(v1,v2,v3);
	//faceNormal = normalize((modelViewMatrix*vec4(faceNormal, 0.0)).xyz);
	faceNormal = normalize(faceNormal);
  for(int i=0; i<3; i++)
  {
    gl_Position = gl_in[i].gl_Position;
    fragColor = geoColor[i];
    fragMaterials = geoMaterials[i];
	fragNormal = faceNormal;
	positionViewSpace_pass = positionViewSpace[i];
    EmitVertex();
  }
  EndPrimitive();
}  
