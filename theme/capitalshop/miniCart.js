/** @format */
document.querySelector(".cart a").addEventListener("click", function () {
  var miniCart = document.querySelector(".mini_cart");
  if (miniCart.style.display === "none" || miniCart.style.display === "") {
      miniCart.style.display = "block"; // Show cart
      showItemsCart(); // Call function to display contents
  } else {
      miniCart.style.display = "none"; // Hide cart
  }
});

var token = localStorage.getItem("Token");
var userId = localStorage.getItem("UserId");

async function showItemsCart() {
  let totalCartPrice = 0; // Initialize total cart price
  let negotiatedPrices = JSON.parse(localStorage.getItem('negotiatedPrices')) || {};

  // Element to display number of items and cart content
  var numberOfSelectedItems = document.getElementById("numberOfSelectedItems");
  var miniCart = document.getElementById("cartGallery");
  miniCart.innerHTML = ''; // Clear previous content

  if (token == null) {
      var selectedItems = JSON.parse(localStorage.getItem("cartItems")) || [];
      numberOfSelectedItems.innerHTML = selectedItems.length;

      selectedItems.forEach((item) => {
          const productId = item.product_id;
          const negotiatedPrice = negotiatedPrices[productId];
          let priceToDisplay;

          // Determine the price to display based on discount or negotiated price
          if (negotiatedPrice) {
              priceToDisplay = parseFloat(negotiatedPrice).toFixed(2);
          } else if (item.priceWithDiscount !== null && item.priceWithDiscount > 0) {
              priceToDisplay = (item.price - (item.price * (item.priceWithDiscount / 100))).toFixed(2);
          } else {
              priceToDisplay = item.price.toFixed(2);
          }

          totalCartPrice += priceToDisplay * item.quantity;

          miniCart.innerHTML += `
              <div class="cart_item">
                  <div class="cart_img">
                      <a href="#"><img src="/Backend/Masterpiece/Masterpiece/Uploads/${item.image}" alt=""/></a>
                  </div>
                  <div class="cart_info">
                      <a href="#">${item.name}</a>
                      <p>${item.quantity} x <span> $${priceToDisplay} </span></p>
                  </div>
                  <div class="cart_remove">
                      <a href="#"><i class="ion-ios-close-outline"></i></a>
                  </div>
              </div>
          `;
      });
  } else {
      let url = `https://localhost:44332/api/Cart/getUserCartItems/${userId}`;
      let request = await fetch(url);

      if (!request.ok) {
          console.error("Failed to fetch cart data:", request.statusText);
          return;
      }

      let data = await request.json();
      numberOfSelectedItems.innerHTML = data.length;

      data.forEach((item) => {
          const productId = item.product.productId;
          const negotiatedPrice = negotiatedPrices[productId];
          let priceToDisplay;

          // Determine the price to display based on discount or negotiated price
          if (negotiatedPrice) {
              priceToDisplay = parseFloat(negotiatedPrice).toFixed(2);
          } else if (item.product.priceWithDiscount !== null && item.product.priceWithDiscount > 0) {
              priceToDisplay = (item.product.price - (item.product.price * (item.product.priceWithDiscount / 100))).toFixed(2);
          } else {
              priceToDisplay = item.product.price.toFixed(2);
          }

          totalCartPrice += priceToDisplay * item.quantity;

          miniCart.innerHTML += `
              <div class="cart_item">
                  <div class="cart_img">
                      <a href="#"><img src="/Backend/Masterpiece/Masterpiece/Uploads/${item.product.image}" alt=""/></a>
                  </div>
                  <div class="cart_info">
                      <a href="#">${item.product.name}</a>
                      <p>${item.quantity} x <span> $${priceToDisplay} </span></p>
                  </div>
                  <div class="cart_remove">
                      <a href="#"><i class="ion-ios-close-outline"></i></a>
                  </div>
              </div>
          `;
      });
  }
}

showItemsCart();
