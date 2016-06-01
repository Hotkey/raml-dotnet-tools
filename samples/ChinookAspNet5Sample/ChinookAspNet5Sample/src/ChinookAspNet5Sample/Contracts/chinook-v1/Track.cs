// Template: Models (ApiModel.t4) version 3.0

// This code was generated by RAML Web Api 2 Scaffolder

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel.DataAnnotations;

namespace ChinookAspNet5Sample.ChinookV1.Models
{
    public partial class Track
    {
        


        public int Id { get; set; }

        [Required]
        public string Name { get; set; }


        public Album Album { get; set; }


        public MediaType MediaType { get; set; }


        public Genre Genre { get; set; }

        [Required]
        public string Composer { get; set; }


        public int Milliseconds { get; set; }


        public int Bytes { get; set; }


        public decimal UnitPrice { get; set; }
    } // end class

} // end Models namespace

