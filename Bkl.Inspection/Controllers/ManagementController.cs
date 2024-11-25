using Bkl.Models;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bkl.Inspection
{
    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class ManagementController : Controller
    {
        [HttpPost("import-camera")]
        public async Task<GeneralResponse> CreateDevice([FromServices] CommonDeviceImport commonDeviceImport, [FromServices] LogonUser user)
        {
            //BklDeviceMetadata meta = await commonDeviceImport.CreateNewDevice(post);
            var result = await this.Request.BodyReader.ReadAsync();
            using var stream = new StreamReader(new MemoryStream(result.Buffer.ToArray()));
            bool success = await commonDeviceImport.ImportCameraDevice(stream, ",");
            return new GeneralResponse { success = success };
        }

        [HttpPost("device")]
        public async Task<BklDeviceMetadata> CreateDevice([FromServices] CommonDeviceImport commonDeviceImport, [FromServices] LogonUser user,
            CreateDeviceRequest post)
        {
            BklDeviceMetadata meta = await commonDeviceImport.CreateNewDevice(post);
            return meta;
        }

        Dictionary<string, string> dic = new Dictionary<string, string>
        {
            {"WindPowerGenerator","风力发电机" },
            {"HeatPowerGenerator","火力发电机" },
        };

        [HttpGet("trees")]
        public IActionResult GetFactoryTree([FromServices] LogonUser user, [FromServices] BklDbContext context, long factoryId, int needdevice = 0)
        {
            var factories = context.BklFactory.Where(s => s.Id == factoryId).ToArray();
            var facilities = context.BklFactoryFacility.Where(s => s.FactoryId == factoryId).ToList();
            var devices = context.BklDeviceMetadata.Where(s => s.FactoryId == factoryId).ToList();

            List<MenuItem> menus = new List<MenuItem>();
            foreach (var facis in facilities.GroupBy(s => s.FacilityType))
            {
                var mitem1 = new MenuItem
                {
                    key = "facigroup-" + facis.Key + "-" + factoryId,
                    label = dic[facis.Key],
                    children = new List<MenuItem>(),
                };
                foreach (var faci in facis)
                {

                    var mfaci = new MenuItem { key = "facility-" + faci.Id, dataType = faci.FacilityType, label = faci.Name };
                    if (needdevice == 1)
                    {

                        var devs = devices.Where(s => s.FacilityId == faci.Id).ToArray();
                        if (devs.Length > 0)
                        {
                            mfaci.children = devs.Select(s => new MenuItem
                            {
                                key = "device-" + s.Id,
                                label = s.DeviceName,
                                dataType = s.DeviceType,
                                data = JsonSerializer.Deserialize<CameraConnectionString>(s.ConnectionString)
                            }).ToList();
                        }



                        //foreach (var gp in devs.GroupBy(s => s.FullPath))
                        //{
                        //    MenuItem mi = new MenuItem
                        //    {
                        //        key = "faci" + faci.Id + "-" + gp.Key,
                        //        dataType = "devgroup",
                        //        label = gp.Key,
                        //        children = gp.Select(s => new MenuItem
                        //        {
                        //            key = "device-" + s.Id,
                        //            label = s.DeviceName,
                        //            dataType = s.DeviceType,
                        //            data = JsonSerializer.Deserialize<CameraConnectionString>(s.ConnectionString)
                        //        }).ToList()
                        //    };
                        //    mfaci.children.Add(mi);
                        //}
                    }
                    //var devs = devices.Where(s => s.FacilityId == faci.Id).ToArray();
                    //if (devs.Length > 0)
                    //{
                    //    mfaci.children = devs.Select(s =>
                    //    {
                    //        return new MenuItem { key = "device-" + s.Id, label = s.DeviceName, data = s };
                    //    }).ToList();
                    //}

                    mitem1.children.Add(mfaci);
                }
                menus.Add(mitem1);
            }
            //menus.Add(new MenuItem { key = "addFactory", label = "添加风场" });
            //menus.Add(new MenuItem { key = "addFacility", label = "添加风机" });
            //menus.Add(new MenuItem { key = "addDevices", label = "添加设备" });
            return Json(menus);
        }


        public class MenuItem
        {
            public string key { get; set; }
            public string label { get; set; }
            public string dataType { get; set; }
            public object data { get; set; }
            public List<MenuItem> children { get; set; }
        }

    }

}
