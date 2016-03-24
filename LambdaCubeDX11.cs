using System;

//------------------------
// LambdaCube DX11 backend

namespace LambdaCube.DX11
{
	using System.Collections.Generic;
	using LambdaCube.IR;
	using data = LambdaCube.IR.data;

	//using DX11 = SharpDX.Direct3D11;
	using DX11 = SlimDX.Direct3D11;
	using DX11Enum = SlimDX.DXGI;
	using DX11Compiler = SlimDX.D3DCompiler;

	enum Primitive
	{
		TriangleStrip,
		TriangleList,
		TriangleFan,
		LineStrip,
		LineList,
		LineLoop,
		PointList}

	;

	enum Type
	{
		FLOAT,
		FLOAT_VEC2,
		FLOAT_VEC3,
		FLOAT_VEC4,
		FLOAT_MAT2,
		FLOAT_MAT3,
		FLOAT_MAT4}

	;

	class Buffer
	{
		List<int> size, byteSize, glType;
		List<int> offset;
		//List<void*> data;
		uint bufferObject;
	}

	class Stream
	{
		Type type;
		Buffer buffer;
		int index;
		bool isArray;
		int glSize;
	}

	class StreamMap
	{
		public Dictionary<string,Stream> map;
	}

	struct UniformValue
	{
		public InputType.Tag tag;
	}

	class Object
	{
		public bool enabled;
		public int order, glMode, glCount;
		public Dictionary<string,UniformValue> uniforms;
		public StreamMap streams;
	}

	class PipelineInput
	{
		public Dictionary<string,List<Object>> objectMap;
		public Dictionary<string,UniformValue> uniforms;
		public int screenWidth, screenHeight;

	}

	struct Texture
	{
		//int target;
		public DX11.Texture2D texture;
	};

	struct StreamInfo
	{
		string name;
		int index;
	};

	class GLProgram
	{
		public DX11.VertexShader vs;
		public DX11.PixelShader ps;
		public DX11.InputLayout inputLayout;

		//public uint program, vertexShader, fragmentShader;
		public Dictionary<string,int> programUniforms, programInTextures;
		public Dictionary<string,StreamInfo> programStreams;
	};

	struct GLStreamData
	{
		int glMode, glCount;
		//StreamMap streams;
	};

	struct Target
	{
		public DX11.RenderTargetView renderView;
		public DX11.DepthStencilView depthView;
	}

	class GLES20Pipeline
	{
		private PipelineInput input;
		private data.Pipeline pipeline;
		private List<Texture> textures;
		private List<Target> targets;
		private List<GLProgram> programs;
		private List<GLStreamData> streamData;
		private int currentProgram;
		private bool hasCurrentProgram;
		public uint screenTarget;
		private Target currentTarget;
		DX11.Viewport viewport;

		// hack: final render texture (back buffer)
		public DX11.RenderTargetView outRenderView;
		public DX11.DepthStencilView outDepthView;
		public DX11.Texture2D outRenderTexture;
		public DX11.Texture2D outDepthTexture;

		GLProgram createProgram (DX11.Device device, Program p_)
		{
			var p = (data.Program)p_;
			var prg = new GLProgram ();
			var bytecodeVS = DX11Compiler.ShaderBytecode.Compile (p.vertexShader, "main", "vs_5_0", DX11Compiler.ShaderFlags.None, DX11Compiler.EffectFlags.None);
			var bytecodePS = DX11Compiler.ShaderBytecode.Compile (p.fragmentShader, "main", "ps_5_0", DX11Compiler.ShaderFlags.None, DX11Compiler.EffectFlags.None);
			prg.vs = new DX11.VertexShader (device, bytecodeVS);
			prg.ps = new DX11.PixelShader (device, bytecodePS);

			prg.inputLayout = new DX11.InputLayout (device, bytecodeVS/*pass.Description.Signature*/, new[] { // TODO
				new DX11.InputElement ("POSITION", 0, DX11Enum.Format.R32G32B32A32_Float, 0, 0),
				new DX11.InputElement ("COLOR", 0, DX11Enum.Format.R32G32B32A32_Float, 16, 0) 
			});

/*
            var bytecode = ShaderBytecode.CompileFromFile("MiniTri.fx", "fx_5_0", ShaderFlags.None, EffectFlags.None);
            var effect = new Effect(device, bytecode);
            var technique = effect.GetTechniqueByIndex(0);
            var pass = technique.GetPassByIndex(0);
            var layout = new InputLayout(device, pass.Description.Signature, new[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0) 
            });
*/
			return prg;
		}

		void createBackBuffer (DX11.Device device, int screenWidth, int screenHeight)
		{
			var depthBufferDesc = new DX11.Texture2DDescription {
				ArraySize = 1,
				BindFlags = DX11.BindFlags.DepthStencil,
				CpuAccessFlags = DX11.CpuAccessFlags.None,
				Format = DX11Enum.Format.D32_Float,
				Height = screenHeight,
				MipLevels = 1,
				OptionFlags = DX11.ResourceOptionFlags.None,
				SampleDescription = new DX11Enum.SampleDescription (1, 0),
				Usage = DX11.ResourceUsage.Default,
				Width = screenWidth
			};
			var renderBufferDesc = new DX11.Texture2DDescription {
				ArraySize = 1,
				BindFlags = DX11.BindFlags.DepthStencil,
				CpuAccessFlags = DX11.CpuAccessFlags.None,
				Format = DX11Enum.Format.R32G32B32A32_Float,
				Height = screenHeight,
				MipLevels = 1,
				OptionFlags = DX11.ResourceOptionFlags.None,
				SampleDescription = new DX11Enum.SampleDescription (1, 0),
				Usage = DX11.ResourceUsage.Default,
				Width = screenWidth
			};

			outDepthTexture = new DX11.Texture2D (device, depthBufferDesc);
			outDepthView = new DX11.DepthStencilView (device, outDepthTexture);
			outRenderTexture = new DX11.Texture2D (device, renderBufferDesc);
			outRenderView = new DX11.RenderTargetView (device, outRenderTexture);
		}

		/*
  = SamplerDescriptor
  { samplerWrapS :: EdgeMode
  , samplerWrapT :: Maybe EdgeMode
  , samplerWrapR :: Maybe EdgeMode
  , samplerMinFilter :: Filter
  , samplerMagFilter :: Filter
  , samplerBorderColor :: Value
  , samplerMinLod :: Maybe Float
  , samplerMaxLod :: Maybe Float
  , samplerLodBias :: Float
  , samplerCompareFunc :: Maybe ComparisonFunction
  }

*/

		Texture createTexture (DX11.Device device, TextureDescriptor t_)
		{
			var td = (data::TextureDescriptor)t_;
			var t = new Texture ();
			var texDesc = new DX11.Texture2DDescription ();
			texDesc.ArraySize = 1;
			texDesc.CpuAccessFlags = DX11.CpuAccessFlags.None;
			if (td.textureSemantic.tag == ImageSemantic.Tag.Color) {
				texDesc.BindFlags = DX11.BindFlags.RenderTarget;
				texDesc.Format = DX11Enum.Format.R32G32B32A32_Float; // TODO
			} else {
				texDesc.BindFlags = DX11.BindFlags.DepthStencil;
				texDesc.Format = DX11Enum.Format.D32_Float;
			}
			var size = (data::VV2U)td.textureSize;
			texDesc.Height = (int)size._0.x;
			texDesc.Width = (int)size._0.y;
			texDesc.MipLevels = td.textureMaxLevel;
			texDesc.OptionFlags = DX11.ResourceOptionFlags.None;
			texDesc.SampleDescription = new DX11Enum.SampleDescription (1, 0);
			texDesc.Usage = DX11.ResourceUsage.Default;

	

			t.texture = new DX11.Texture2D (device, texDesc);
			return t;
		}

		Target createRenderTarget (DX11.Device device, RenderTarget t_)
		{
			var t = (data.RenderTarget)(t_);
			var tg = new Target ();
			foreach (var i_ in t.renderTargets) {
				var i = (data.TargetItem)i_;
				if (i.targetRef.valid) {
					int idx = ((data.TextureImage)i.targetRef.data)._0;
					if (i.targetSemantic.tag == ImageSemantic.Tag.Color) {
						tg.renderView = new DX11.RenderTargetView (device, textures [idx].texture);
					} else {
						tg.depthView = new DX11.DepthStencilView (device, textures [idx].texture);
					}
				} else {
					if (i.targetSemantic.tag == ImageSemantic.Tag.Color) {
						tg.renderView = outRenderView;
					} else {
						tg.depthView = outDepthView;
					}
				}
			}
			return tg;
		}

		GLES20Pipeline (DX11.Device device, Pipeline ppl_, int screenWidth, int screenHeight)
		{
			viewport = new DX11.Viewport ();
			screenTarget = 0;
			hasCurrentProgram = false;
			var ppl = (data.Pipeline)ppl_;
			pipeline = ppl;
			if (ppl.backend.tag != Backend.Tag.WebGL1) {
				throw new Exception ("unsupported backend");
			}
			createBackBuffer (device, screenWidth, screenHeight);
			foreach (var i in ppl.textures) {
				textures.Add (createTexture (device, i));
			}
			foreach (var i in ppl.targets) {
				targets.Add (createRenderTarget (device, i));
			}
			foreach (var i in ppl.programs) {
				programs.Add (createProgram (device, i));
			}
			foreach (var i in ppl.streams) {
				//streamData.Add(createStreamData(i));
			}
		}

		~GLES20Pipeline ()
		{
		}

		void setPipelineInput (PipelineInput i)
		{
		}

		void setupRasterContext (DX11.DeviceContext context)
		{
      
		}

		void render (DX11.DeviceContext context)
		{
			foreach (var i in pipeline.commands) {
				switch (i.tag) {
				case Command.Tag.SetRasterContext: // TODO
					{
						var cmd = (data.SetRasterContext)i;
						//setupRasterContext(cmd->_0);
						break;
					}
				case Command.Tag.SetAccumulationContext: // TODO
					{
						var cmd = (data.SetAccumulationContext)i;
						//setupAccumulationContext(cmd->_0);
						break;
					}
				case Command.Tag.SetTexture:
					{
						var cmd = (data.SetTexture)i;
						//glActiveTexture(GL_TEXTURE0 + cmd->_0);
						//glBindTexture(textures[cmd->_1].target, textures[cmd->_1].texture);
						break;
					}
				case Command.Tag.SetProgram:
					{
						var cmd = (data.SetProgram)i;
						hasCurrentProgram = true;
						currentProgram = cmd._0;
						var prg = programs [currentProgram];
						context.InputAssembler.InputLayout = prg.inputLayout;
						context.VertexShader.Set (prg.vs);
						context.PixelShader.Set (prg.ps);
						break;
					}
				case Command.Tag.SetRenderTarget:
					{
						var cmd = (data::SetRenderTarget)i;
						Target t = targets [cmd._0];
						currentTarget = t;
						context.OutputMerger.SetTargets (t.depthView, t.renderView);
						if (input != null) {
							viewport.X = 0;
							viewport.Y = 0;
							viewport.Width = input.screenWidth;
							viewport.Height = input.screenHeight;
							context.Rasterizer.SetViewports (viewport);
						}
						break;
					}
				case Command.Tag.ClearRenderTarget:
					{
						var cmd = (data.ClearRenderTarget)i;
						SlimDX.Color4 color = new SlimDX.Color4 (0, 0, 0, 1);
						float depth = 0;
						bool hasDepth = false;
						bool hasColor = false;
						foreach (var a in cmd._0) {
							var image = (data.ClearImage)a;
							switch (image.imageSemantic.tag) {
							case ImageSemantic.Tag.Depth:
								{
									var v = (data.VFloat)image.clearValue;
									depth = v._0;
									hasDepth = true;
									break;
								}
							case ImageSemantic.Tag.Stencil:
								{
									var v = (data.VWord)image.clearValue; // TODO
									break;
								}
							case ImageSemantic.Tag.Color:
								{
									switch (image.clearValue.tag) {
									case Value.Tag.VFloat:
										{
											var v = (data.VFloat)image.clearValue;
											hasColor = true;
											color.Red = v._0;
											break;
										}
									case Value.Tag.VV2F:
										{
											var v = (data.VV2F)image.clearValue;
											hasColor = true;
											color.Red = v._0.x;
											color.Green = v._0.y;
											break;
										}
									case Value.Tag.VV3F:
										{
											var v = (data.VV3F)image.clearValue;
											color.Red = v._0.x;
											color.Green = v._0.y;
											color.Blue = v._0.z;
											break;
										}
									case Value.Tag.VV4F:
										{
											var v = (data.VV4F)image.clearValue;
											color.Red = v._0.x;
											color.Green = v._0.y;
											color.Blue = v._0.z;
											color.Alpha = v._0.w;
											break;
										}
									default:
										break;
									}
									break;
								}
							}
						}
						if (hasColor) { 
							context.ClearRenderTargetView (currentTarget.renderView, color);
						}
						if (hasDepth) { 
							context.ClearDepthStencilView (currentTarget.depthView, DX11.DepthStencilClearFlags.Depth, depth, 0);
						}
						break;
					}
				case Command.Tag.SetSamplerUniform: // TODO
					{
						if (hasCurrentProgram) {
							var cmd = (data.SetSamplerUniform)i;
							int sampler = programs [currentProgram].programInTextures [cmd._0];
							//glUniform1i(sampler, cmd->_1);
						}
						break;
					}
				case Command.Tag.RenderSlot: // TODO
					{
						if (input != null && pipeline != null && hasCurrentProgram) {
							var cmd = (data.RenderSlot)i;
							var slot = (data.Slot)pipeline.slots [cmd._0];
							if (!input.objectMap.ContainsKey (slot.slotName)) {
								break;
							}
							foreach (var o in input.objectMap[slot.slotName]) {
								if (!o.enabled) {
									continue;
								}
								foreach (var u in programs[currentProgram].programUniforms) {
									if (o.uniforms.ContainsKey (u.Key)) {
										//setUniformValue(u.second, o->uniforms[u.first]);
									} else {
										//setUniformValue(u.second, input->uniforms[u.first]);
									}
								}
								foreach (var s in programs[currentProgram].programStreams) {
									//setStream(s.second.index, *o->streams->map[s.second.name]);
								}
								//glDrawArrays(o->glMode, 0, o->glCount);
							}
						}
						break;
					}
				case Command.Tag.RenderStream: // TODO
					{
						if (input != null && pipeline != null && hasCurrentProgram) {
							var cmd = (data.RenderStream)i;
							GLStreamData data = streamData [cmd._0];
							foreach (var s in programs[currentProgram].programStreams) {
								//setStream(s.second.index, *data->streams.map[s.second.name]);
							}
							//glDrawArrays(data->glMode, 0, data->glCount);
						}
						break;
					}
				}
			}

		}
	};

}
