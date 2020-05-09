# Iguazu

Iguazu est une application Windows permettant de transcrire un fichier audio en texte. Pour cela, elle utilise les service Google Speech-to-Text et Google Cloud Storage.

## Configuration requise

- Windows 64 bits.
- [.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1).

## Utilisation

Pour utiliser l’application, il faut posséder un compte Google et avoir activer les services suivants :
- Cloud Sppech-to-text API ;
- Cloud Storage.

### Création d’un utilisateur

Dans votre espace Google Cloud Platform, vous devez créer un compte de service.

Pour cela, allez dans le menu *API et services* > *Identifiants*. Ensuite, cliquez sur *Créer des identifiants*, puis *Compte de service*. Renseignez les champs nécessaires et validez. Il n’est pas nécessaire d’ajouter de rôle, vous pouvez donc cliquer sur *Continuer*.

Créez ensuite une clé en cliquant sur le bouton *Créer une clé*. Choisissez le type de clé *JSON* puis validez sur *Créer*. Un fichier JSON va alors être téléchargé sur votre PC. Il faudra ensuite le stocker dans le répertoire de votre choix, puis indiquer son emplacement dans les paramètres d’Iguazu (champ *Fichier des identifiants Google*).

Note : un redémarrage d’Iguazu est nécessaire pour que le fichier JSON soit pris en compte.

### Configuration de Google Cloud Storage

Vous devez créer un bucket et ajouter les rôles suivants à l’utilisateur créé précédemment : *Cloud Storage* / *Administrateur des objets de l'espace de stockage*.

Vous devez ensuite indiquer le nom du bucket dans les préférences d’Iguazu (champ *Nom du bucket*).
