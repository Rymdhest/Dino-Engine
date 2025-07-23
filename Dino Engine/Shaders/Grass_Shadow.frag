#version 420

in float valid;

void main() {
	if (valid < 0.5f) discard;
}