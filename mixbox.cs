using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public static class Mixbox {

	const int NUMLATENTS = 7;
	public static int[] c_table;

	public struct Latent {
		public float[] vals;

		public float this[int index]
		{
			get => vals[index];
			set => vals[index] = value;
		}

		public static implicit operator Latent(float[] floatArr) {
			if(floatArr.Length != 7) throw new Exception("Invalid");
			Latent r = new Latent();
			r.vals = floatArr;
			return r;
		}

		public static implicit operator float[](Latent latent) {
			return latent.vals;
		}
	}

	public struct RGB {
		float[] vals;
		public float r => vals[0];
		public float g => vals[1];
		public float b => vals[2];

		public RGB(float R, float G, float B) {
			vals = new float[]{R,G,B};
		}

		public float this[int index]
		{
			get => vals[index];
			set => vals[index] = value;
		}

		public static implicit operator RGB(float[] floatArr) {
			if(floatArr.Length != 3) throw new Exception("Invalid");
			RGB c = new RGB();
			c.vals = floatArr;
			return c;
		}

		public static implicit operator float[](RGB rgb) {
			return rgb.vals;
		}
	}

	static float[,] coefs = new float[,]{
		{1.0f * +0.07717053f, 1.0f * +0.02826978f, 1.0f * +0.24832992f},
		{1.0f * +0.95912302f, 1.0f * +0.80256528f, 1.0f * +0.03561839f},
		{1.0f * +0.74683774f, 1.0f * +0.04868586f, 1.0f * +0.0f},
		{1.0f * +0.99518138f, 1.0f * +0.99978149f, 1.0f * +0.99704802f},
		{3.0f * +0.01606382f, 3.0f * +0.27787927f, 3.0f * +0.10838459f},
		{3.0f * -0.2271565f , 3.0f * +0.48702601f, 3.0f * +0.35660312f},
		{3.0f * +0.09019473f, 3.0f * -0.0510829f , 3.0f * +0.66245019f},
		{3.0f * +0.26826063f, 3.0f * +0.2236457f , 3.0f * +0.061415f},
		{3.0f * -0.11677001f, 3.0f * +0.45951942f, 3.0f * +1.22955f},
		{3.0f * +0.35042682f, 3.0f * +0.65938413f, 3.0f * +0.94329691f},
		{3.0f * +1.07202375f, 3.0f * +0.27090076f, 3.0f * +0.34461513f},
		{3.0f * +0.92964458f, 3.0f * +0.13855183f, 3.0f * -0.01495765f},
		{3.0f * +1.00720859f, 3.0f * +0.85124701f, 3.0f * +0.10922038f},
		{3.0f * +0.98374897f, 3.0f * +0.93733704f, 3.0f * +0.39192814f},
		{3.0f * +0.94225681f, 3.0f * +0.26644346f, 3.0f * +0.60571754f},
		{3.0f * +0.99897033f, 3.0f * +0.40864351f, 3.0f * +0.60217887f},
		{6.0f * +0.31232351f, 6.0f * +0.34171197f, 6.0f * -0.04972666f},
		{6.0f * +0.42768261f, 6.0f * +1.17238033f, 6.0f * +0.10429229f},
		{6.0f * +0.68054914f, 6.0f * -0.23401393f, 6.0f * +0.35832587f},
		{6.0f * +1.00013113f, 6.0f * +0.42592007f, 6.0f * +0.31789917f},
	};

	static float Clamp(float x, float xmin, float xmax) {
		return Math.Min(Math.Max(x, xmin), xmax);
	}

	static void Mix(float[] c, RGB RGB) {
		var c00 = c[0] * c[0];
		var c11 = c[1] * c[1];
		var c22 = c[2] * c[2];
		var c33 = c[3] * c[3];
		var c01 = c[0] * c[1];
		var c02 = c[0] * c[2];

		float[] weights = new float[]{
			c[0] * c00,
			c[1] * c11,
			c[2] * c22,
			c[3] * c33,
			c00 * c[1],
			c01 * c[1],
			c00 * c[2],
			c02 * c[2],
			c00 * c[3],
			c[0] * c33,
			c11 * c[2],
			c[1] * c22,
			c11 * c[3],
			c[1] * c33,
			c22 * c[3],
			c[2] * c33,
			c01 * c[2],
			c01 * c[3],
			c02 * c[3],
			c[1] * c[2] * c[3],
		};

		for (int i = 0; i < 3; i++) {
			RGB[i] = 0.0f;
		}

		for (int j = 0; j < 20; j++) {
			for (int i = 0; i < 3; i++) {
				RGB[i] += weights[j] * coefs[j,i];
			}
		}
	}

	public static RGB Lerp_sRGB8(RGB RGB1, RGB RGB2, float t) {
		Latent Latent_A = sRGB8_to_latent(RGB1.r, RGB1.b, RGB1.g);
		Latent Latent_B = sRGB8_to_latent(RGB2.r, RGB2.b, RGB2.g);
		Latent Latent_C = new float[]{0, 0, 0, 0, 0, 0, 0};
		for (int l = 0; l < NUMLATENTS; ++l) {
			Latent_C[l] = (1.0f - t) * Latent_A[l] + t * Latent_B[l];
		}
		return Latent_to_sRGB8(Latent_C);
	}

	
	public static Latent sRGB8_to_latent(RGB rgb) {
		return sRGB8_to_latent(rgb.r, rgb.g, rgb.b);
	}

	public static Latent sRGB8_to_latent(float r, float g, float b) {
		int offset = ((int)r + (int)(g * 257) + (int)(b * 257 * 257)) * 3;

		float[] c = new float[]{0, 0, 0, 0};
		c[0] = c_table[offset + 0] / 255.0f;
		c[1] = c_table[offset + 1] / 255.0f;
		c[2] = c_table[offset + 2] / 255.0f;
		c[3] = 1.0f - (c[0] + c[1] + c[2]);

		RGB mixRGB = new RGB(0, 0, 0);
		Mix(c, mixRGB);
		return new float[]{c[0], c[1], c[2], c[3], r / 255.0f - mixRGB[0], g / 255.0f - mixRGB[1], b / 255.0f - mixRGB[2]};
	}

	public static RGB Latent_to_sRGB8(Latent latent) {
		return Latent_to_sRGB8_dither(latent, 0, 0, 0);
	}

	public static RGB Latent_to_sRGB8_dither(Latent latent, float dither_r, float dither_g, float dither_b) {
		RGB c = Latent_to_sRGB32f(latent);
		return new RGB(
			(float)Math.Floor(Clamp((float)Math.Round(c.r * 255.0f + dither_r), 0, 255)),
			(float)Math.Floor(Clamp((float)Math.Round(c.g * 255.0f + dither_g), 0, 255)),
			(float)Math.Floor(Clamp((float)Math.Round(c.b * 255.0f + dither_b), 0, 255))
		);
	}

	public static RGB Latent_to_sRGB32f(Latent latent) {
		RGB RGB = new RGB(0, 0, 0);
		Mix(latent, RGB);
		return new RGB(
			Clamp(RGB.r + latent[4], 0.0f, 1.0f),
			Clamp(RGB.g + latent[5], 0.0f, 1.0f),
			Clamp(RGB.b + latent[6], 0.0f, 1.0f)
		);
	}

	/// <summary>
	/// Takes lut file data (4096x4096) as ARGB ints
	/// </summary>
	public static void Init(int[] imageDataArray) {
		c_table = new int[257 * 257 * 257 * 3];
		for (int b = 0; b < 256; b++) {
			for (int g = 0; g < 256; g++) {
				for (int r = 0; r < 256; r++) {
					var x = (b % 16) * 256 + r;
					var y = (b / 16) * 256 + g;
					for (int i = 0; i < 3; i++) {
						int rawCol = imageDataArray[(x + y * 256 * 16)];
						int[] dat = new int[] { (rawCol>>0)&255, (rawCol>>8)&255,(rawCol>>16)&255,(rawCol>>24)&255 };
						c_table[(r + g * 257 + b * 257 * 257) * 3 + i] = dat[i];
					}
				}
			}
		}
	}
}