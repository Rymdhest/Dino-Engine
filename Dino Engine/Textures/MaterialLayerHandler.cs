using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Textures
{
    public class MaterialLayerHandler
    {
        private List <MaterialLayer> _materialLayers = new List<MaterialLayer>();
        public MaterialLayerHandler()
        {

        }
        public void addLayer(MaterialLayer layer)
        {
            _materialLayers.Add(layer);
        }
        public void removeLayer(MaterialLayer layer)
        {
            _materialLayers.Remove(layer);
        }

        public void cleanUp()
        {
            foreach(MaterialLayer layer in _materialLayers)
            {
                layer.CleanUp();
            }
            _materialLayers.Clear();
        }
    }
}
