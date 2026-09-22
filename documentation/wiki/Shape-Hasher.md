The Shape Hasher is a crucial part of the Type Mapping process, the process that teaches DeepLynx how to interpret and store your data using its ontology. The shape hasher’s primary goal is to identify distinct shapes in your data which are used to determine how type mappings are applied. The shape hasher uses a sha256 base64 hasher to compile and hash the “shape” of your object. It does this by compiling all the property keys (including nested keys) and their types into a string, and then hashing it. This gives us a unique string identifier for the “shape” of your object. Below are some examples of data being read into DeepLynx with explanations of how the shape hasher would analyze them to determine their shapes. 

## Basic Example

```
[
    {
       "objectId": 1,
       "name": "EWR-9154-Beartooth_AR_2022_3DView_1:1",
       "creationDate": "2022-May-31 00:00:00"
    },
    {
       "objectId": 2,
       "name": "BOB",
       "creationDate": "2022-May-31 00:00:00"
    },
    {
       "objectId": 3,
       "name": "EWR-9154-Beartooth_AR_2022_3DView_3:1",
       "creationDate": "2022-May-31 00:00:00"
    }
 ]
```

 
 

 

In the example above, can you guess how many shapes would be returned? The answer is just 1! When the shape hasher enters this list of objects, it breaks down each object individually. It sees that the shape of the first object it hashes is “objectId:number name:string creationDate:string” and therefore returns three of the same hashes.

## Handling Null Values 

In the cases of null values, this hasher treats those instances as their own data type. Previously, we had the hasher match an object with a null value to an object with a similar shape similar shape with a typed value. However, since this can cause an inaccurate assumption in the instance we have multiple objects with similar shapes but varying data types, we have since made null values their own distinct shape. 

 

```
[
   {
      "objectId": 1,
      "name": 3,
      "creationDate": "2022-May-31 00:00:00"
   },
   {
      "objectId": 2,
      "name": null,
      "creationDate": "2022-May-31 00:00:00"
   },
   {
      "objectId": 3,
      "name": "EWR-9154-Beartooth_AR_2022_3DView_3:1",
      "creationDate": "2022-May-31 00:00:00"
   }
]
```

 

In the example above, can you guess how many shapes will be returned? The correct answer is 3! While we have three shapes with all the same keys, the keys’ associated data types vary. In the first hash, the hash for the key name would be “name:number”. In the second one it would be “name:null”. And in the final one it would be “name:string”. This would cause three different hash values and so three different shapes would be returned. 

## Ignoring Properties and Type vs Value 

We can also have specials cases utilizing the stop or value node functionality, also known as the “ignored properties” and “type vs value” property on the user interface. These properties are provided in the advanced options when creating or editing a data source or can be specified through the user interface when creating a data source by supplying “stop_nodes:” and “value_nodes:” configurations.

<img width="1667" alt="Wiki Photo ADD" src="https://github.com/idaholab/Deep-Lynx/assets/156716546/cfc5902e-735b-44a6-8868-f390a6af7ca2">

The value node notifies the hasher to treat the value in a key-value pair as the contents of the value rather than using the data type as the value. The user usually passes in a path to a specific key-value pair as input in order to identify a key as a value node.  

 

```
[
  {
      "car": {
          "id": "UUID",
          "name": "test car",
          "manufacturer": {
              "id": "UUID",
              "name": "Test Cars Inc",
              "location": "Seattle, WA"
          },
          "tire_pressures": [
              {
                "id": "tire0",
                "measurement_unit": "PSI",
                "measurement": 35.08,
                "measurement_name": "tire pressure"
              }
          ]
      },
      "car_maintenance": {
        "id": "UUID",
        "name": "test cars maintenance",
        "start_date": "1/1/2020 12:00:00",
        "average_visits_per_year": 4,
        "maintenance_entries": [
              {
                "id": 1,
                "check_engine_light_flag": true,
                "type": "oil change",
                "parts_list": [
                      {
                        "id": "oil",
                        "name": "synthetic oil",
                        "price": 45.66,
                        "quantity": 1
                      }
                  ]
              },
              {
                "id": 2,
                "check_engine_light_flag": false,
                "type": "tire rotation",
                "parts_list": [
                      {
                        "id": "tire",
                        "name": "all terrain tire",
                        "price": 150.99,
                        "quantity": 4
                      }
                  ]
              }
          ]
      }
  },
  {
      "car": {
          "id": "Honda",
          "name": "test car",
          "manufacturer": {
              "id": "UUID",
              "name": "Test Cars Inc",
              "location": "Seattle, WA"
          },
          "tire_pressures": [
              {
                "id": "tire0",
                "measurement_unit": "PSI",
                "measurement": 35.08,
                "measurement_name": "tire pressure"
              }
          ]
      },
      "car_maintenance": {
        "id": "UUID",
        "name": "test cars maintenance",
        "start_date": "1/1/2020 12:00:00",
        "average_visits_per_year": 4,
        "maintenance_entries": [
              {
                "id": 1,
                "check_engine_light_flag": true,
                "type": "oil change",
                "parts_list": [
                      {
                        "id": "oil",
                        "name": "synthetic oil",
                        "price": 45.66,
                        "quantity": 1
                      }
                  ]
              },
              {
                "id": 2,
                "check_engine_light_flag": false,
                "type": "tire rotation",
                "parts_list": [
                      {
                        "id": "tire",
                        "name": "all terrain tire",
                        "price": 150.99,
                        "quantity": 4
                      }
                  ]
              }
          ]
      }
  }
]
```

 

For example, in the JSON file above, the shape hasher would typically return a single shape from this file, as the contents under both instances of “car” have the same keys and the same data types for the values. However, if a user were to pass in “car.id” as the input for value nodes, we would get two distinct shapes. This is because the shape hasher now reads car.id as two separate values, ‘id:UUID’ and ‘id:Honda’. 

 

The stop node case on the other hand, notifies the hasher to ignore a certain key-value pair, based off the users input. Instead of a path to a key, the user simply just needs to pass in the name of the key and all instances of that key are ignored.  

 

```
[
   {
      "objectId": 1,
      "name": "EWR-9154-Beartooth_AR_2022_3DView_1:1",
      "nickname": "kobe",
      "creationDate": "2022-May-31 00:00:00"
   },
   {
      "objectId": 2,
      "name": "EWR-9154-Beartooth_AR_2022_3DView_2:1",
      "creationDate": "2022-May-31 00:00:00",
      "days": 4
   },
   {
      "objectId": 3,
      "name": "EWR-9154-Beartooth_AR_2022_3DView_3:1",
      "creationDate": "2022-May-31 00:00:00"
   }
]
```

 

In the above example, three shapes would be formed, as “nickname” and “days” are unique keys in two of the shapes respectively. However, if a user were to pass the stop nodes of “days” and “nickname” into DeepLynx, the shape-hasher would then ignore those two key-value pairs, resulting in one shape being returned with the keys and value types of objectId, name and creationDate only.  


## Simplifier Functionality 

As the shape hasher steps into each layer of a JSON list passed in, it provides a hash for each portion of an object, whether nested or not. These hashes are then passed into a hashset to provide all the hash values of each unique hash (and shape) in the JSON file passed in. This will return the unique hashes for each distinct shape and will also notify you of how many unique shapes exist. 

 

```
[
    [
        {"id": "1"},
        {"id": "2"}
    ],
    [
        {"id": "3"}
    ]
]
```

 

Can you guess how many shapes would be returned in this file? While it might have been tempting to say two, the correct answer is one! This is because of how the hashset works in each layer. While yes, these shapes are definitely different in size, the data types of each one are the exact same.  

 

## Nested, Mismatched and Empty Arrays 

In our previous shape hasher, empty arrays and objects would sometimes cause different shapes to be matched as the same, making one or more of the shapes unmappable. This new hasher now ensures the empty arrays or objects are recognized and do not cause unique shapes to go unmapped.  

 

```
[
    {
        "ManufacturingProcess": {
            "keys": {
                  "id": "ab9c492-b3f0-412b-ae68-c6dc2e21127d",
                  "name": "Test process",
                  "description": "an example"
            },
            "children": {
                  "ArrayOne": [],
                  "ArrayTwo": [{"id": "123"}, {"id": "456"}]
            }
        }
     },
 
     {
        "ManufacturingProcess": {
            "keys": {
                  "id": "ab9c492-b3f0-412b-ae68-c6dc2e21127d",
                  "name": "Test process",
                  "description": "an example"
            },
            "children": {
                  "ArrayOne": [{"id": "123"}, {"id": "456"}],
                  "ArrayTwo": []
            }
        }
     }
 ]
```

In the example above, can you guess how many unique shapes the new hasher would return? The correct answer is two. The old hasher would have ignored the empty nested arrays, either “ArrayOne” or “ArrayTwo”, and mapped both instances of “ManufacturingProcess” as the same shape. Within this new hasher, the function notifies the hasher there is an empty array or object, ensuring that these two distinct shapes are both mappable and recognized.  

 

 Once a shape hash is created, DeepLynx will look for a type mapping record with the matching shape hash and if none exist a new one will be created. Then, from the corresponding record, the according transformations are performed on the data.  