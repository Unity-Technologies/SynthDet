namespace SynthDet
{
    public static class AddressablesConfiguration
    {
        public static string remoteLoadPath = "http://localhost:8000/StandaloneWindows64";
        public static string catalogUrl = "http://localhost:8888/StandaloneWindows64/catalog_2021.03.21.03.33.58.json";
        public static string signedCatalogUrl = "https://storage.googleapis.com/steven-addressables-tests/StandaloneWindows64/catalog_2021.03.21.03.33.58.json?Expires=1616397276&GoogleAccessId=bhram-test-usc1-taquito%40unity-ml-bhram-test.iam.gserviceaccount.com&Signature=egXwjAJ0fa%2Btv4EjOWHKJ5rhWLNusofJTq1js%2Fl7WTOziIVN3PB1POrrYiOdVydrEl4tsZlu55P96pyQdZaQnrQdaoHcQy5Q44H%2FdZtuZ7Kxpor%2BLgm6UGo%2FvQkW1yZeQe3A8Q7p8aZ4FmmnuKE07mvJ9otp8ZN%2F5eWlNl3ffo57Fz30iRC7cWMEiFihCLitiZPyRs%2FtAl9vwsZAyMxaYdlhJ2k0tZ8dHo0V1hmgREjboYeCZnp1FClpSlZPw9OL9V0PqS4z%2F016Ttn3XHeg2jmHLOBT6rILHYzjHkOO0985NLvpzqsL7V0HavGlKMFWMUA5qCGd9GJ26C0ZCPWKPw%3D%3D";
        const string k_DefaultRemoteLoadPath = "http://localhost:8000/[BuildTarget]";
    }
}
