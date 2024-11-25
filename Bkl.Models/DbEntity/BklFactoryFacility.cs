using MySql.EntityFrameworkCore.DataAnnotations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Bkl.Models
{
    [Table("bkl_factory_facility")]
    
    
    public partial class BklFactoryFacility
    {
        public BklFactoryFacility()
        {
            GPSLocation = "";
            Createtime = DateTime.Now;
            Id = SnowId.NextId();
        }
        [Key,DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }
        [MaxLength(100), Required] public string Name { get; set; }
        [MaxLength(30), Required]  public string FacilityType { get; set; }
        [MaxLength(100), Required] public string FactoryName { get; set; }
        [Required]  public long FactoryId { get; set; }
        [Required] public long CreatorId { get; set; }
        [MaxLength(30), Required] public string CreatorName { get; set; }
        [Required] public DateTime Createtime { get; set; }
        //latidude longitude 
        [MaxLength(50), Required] public string GPSLocation { get; set; }
    }
}
