using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Plugin.CurrentActivity;
using Plugin.Media;
using System;
using System.IO;
using Android.Graphics;
using ZXing;
using ZXing.QrCode;
using ZXing.Common;
using ZXing.Mobile;

namespace AppAlmacenaNoSQLBlobs
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        string Archivo, rutablob;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            //Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            SupportActionBar.Hide();
            CrossCurrentActivity.Current.Init(this, savedInstanceState);
            var Imagen = FindViewById<ImageView>(Resource.Id.image);
            var btnAlmacenar = FindViewById<Button>(Resource.Id.btnalmacenar);
            var txtNombre = FindViewById<EditText>(Resource.Id.txtnombre);
            var txtDomicilio = FindViewById<EditText>(Resource.Id.txtdomicilio);
            var txtCorreo = FindViewById<EditText>(Resource.Id.txtcorreo);
            var txtEdad = FindViewById<EditText>(Resource.Id.txtedad);
            var txtSaldo = FindViewById<EditText>(Resource.Id.txtsaldo);
            txtNombre.RequestFocus();
            Imagen.Click += async delegate
            {
                await CrossMedia.Current.Initialize();
                var archivo = await CrossMedia.Current.TakePhotoAsync(new Plugin.Media.Abstractions.StoreCameraMediaOptions
                {
                    Directory = "Imágenes",
                    Name = txtNombre.Text,
                    SaveToAlbum = true,
                    CompressionQuality = 30,
                    CustomPhotoSize = 30,
                    PhotoSize = Plugin.Media.Abstractions.PhotoSize.Medium,
                    DefaultCamera = Plugin.Media.Abstractions.CameraDevice.Rear
                });
                if (archivo == null)
                    return;
                Bitmap bp = BitmapFactory.DecodeStream(archivo.GetStream());
                Archivo = System.IO.Path.Combine(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), txtNombre.Text + ".jpg"));
                var stream = new FileStream(Archivo, FileMode.Create);
                bp.Compress(Bitmap.CompressFormat.Jpeg, 30, stream);
                stream.Close();
                Imagen.SetImageBitmap(bp);
                long memoria1 = GC.GetTotalMemory(false);
                Toast.MakeText(this.ApplicationContext, memoria1.ToString(), ToastLength.Long).Show();
                GC.Collect();
                long memoria2 = GC.GetTotalMemory(false);
                Toast.MakeText(this.ApplicationContext, memoria2.ToString(), ToastLength.Long).Show();
            };
            btnAlmacenar.Click += async delegate
            {
                try
                {
                    var CuentadeAlmacenamiento = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=enriqueaguilar;AccountKey=zvor3ixApqcKKWJj5nk2tDmrSNinlSk6W0stEnAeBRyZdZAVYpp9J/Ag/lAamkKBaJP0VK4GXt4/DnVN6CU4YQ==;EndpointSuffix=core.windows.net");
                    var ClienteBlob = CuentadeAlmacenamiento.CreateCloudBlobClient();
                    var Carpeta = ClienteBlob.GetContainerReference("imagenes");
                    var resourceBlob = Carpeta.GetBlockBlobReference(txtNombre.Text + ".jpg");
                    await resourceBlob.UploadFromFileAsync(Archivo.ToString());
                    Toast.MakeText(this.ApplicationContext, "Imagen almacenada en contenedor de Blobs", ToastLength.Long).Show();

                    rutablob = resourceBlob.StorageUri.PrimaryUri.ToString();
                    Writer writer = new QRCodeWriter();
                    BitMatrix bm = writer.encode(rutablob, BarcodeFormat.QR_CODE, 600, 600);
                    BitmapRenderer bit = new BitmapRenderer();
                    var imagenqr = bit.Render(bm, BarcodeFormat.QR_CODE, rutablob);
                    Imagen.SetImageBitmap(imagenqr);

                    var TablaNoSQL = CuentadeAlmacenamiento.CreateCloudTableClient();
                    var Coleccion = TablaNoSQL.GetTableReference("Registros");
                    await Coleccion.CreateIfNotExistsAsync();
                    var clientes = new Clientes("Clientes con Imágenes", txtNombre.Text);
                    clientes.Correo = txtCorreo.Text;
                    clientes.Saldo = double.Parse(txtSaldo.Text);
                    clientes.Edad = int.Parse(txtEdad.Text);
                    clientes.Domicilio = txtDomicilio.Text;
                    clientes.ImagenBlob = txtNombre.Text + ".jpg";
                    var Almacena = TableOperation.Insert(clientes);
                    await Coleccion.ExecuteAsync(Almacena);
                    Toast.MakeText(this.ApplicationContext, "Datos almacenados en Tabla NoSQL", ToastLength.Long).Show();
                } catch (Exception ex)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
                }
            };
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
        {
            Plugin.Permissions.PermissionsImplementation.Current.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
    public class Clientes : TableEntity
    {
        public Clientes(string Categoria, string Nombre)
        {
            PartitionKey = Categoria;
            RowKey = Nombre;
        }
        public string Correo { get; set; }
        public int Edad { get; set; }
        public string Domicilio { get; set; }
        public double Saldo { get; set; }
        public string ImagenBlob { get; set; }
    }
}